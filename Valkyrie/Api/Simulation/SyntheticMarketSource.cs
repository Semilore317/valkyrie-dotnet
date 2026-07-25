using Microsoft.Extensions.Options;
using Valkyrie.Api.Dto;
using Valkyrie.Core.Configuration;
using Valkyrie.Logging;
using Valkyrie.Orders;

namespace Valkyrie.Api.Simulation;

public class SyntheticMarketSource(
    OrderGateway gateway,
    ITextLogger logger,
    IOptions<MarketSimulatorConfiguration> config
) : IMarketDataSource
{
    private readonly MarketSimulatorConfiguration _config = config.Value;

    public string Name => "Synthetic Simulator";

    public Task RunAsync(CancellationToken token)
    {
        logger.Info("MarketSimulator", $"starting for {_config.Instruments.Count} book(s)");

        // one loop per instrument, run concurrently
        var loops = _config
            .Instruments
            .Select(instrument => RunInstrument(instrument, token));
        return Task.WhenAll(loops);
    }

    private void AddLiquidity(SimulatedInstrument instrument, long fair, Random rng, List<(long, Side)> resting)
    {
        var buy = rng.NextDouble() < 0.5;
        var away = (rng.Next(1, instrument.BookDepth + 1)) * instrument.TickSize;
        var price = buy
            ? fair - away
            : fair + away;

        var quantity = (uint)(rng.Next(1, 12) * 20);

        var side = buy
            ? Side.Buy
            : Side.Sell;

        var ack = gateway.Submit(
            new PlaceOrderRequest(
                instrument.SecurityId,
                _config.Username,
                side,
                price,
                quantity
            ));
        
        if(HasRestingRemainder(ack, side, quantity))
            resting.Add((ack.OrderId, side));
    }

    private void PullLiquidity(
        SimulatedInstrument instrument,
        Random rng,
        List<(long id, Side side)> resting
    )
    {
        if (resting.Count == 0)
            return;

        var i = rng.Next(resting.Count);
        gateway.Cancel(
            resting[i].id,
            instrument.SecurityId,
            _config.Username
        );
        resting.RemoveAt(i);
    }

    private void Aggress(SimulatedInstrument instrument, Random rng)
    {
        if (!gateway.TryGetBook(instrument.SecurityId, out var book) || book == null)
            return;

        var buy = rng.NextDouble() < 0.5;
        long? target = buy ? book.Ask : book.Bid;

        if (target == null)
            return;

        uint quantity = (uint)(rng.Next(1, 6) * 20);
        var side = buy ? Side.Buy : Side.Sell;
        gateway.Submit(new PlaceOrderRequest(
            instrument.SecurityId,
            _config.Username,
            side,
            target.Value,
            quantity
        ));
    }

    /// <summary>
    /// implements a Geometric Brownian Motion(GBM) step with standard normal sampling (Box-Muller transform)
    /// combined with occasional Poisson-style jumps
    /// </summary>
    private static long RandomWalk(long fair, SimulatedInstrument? instrument, Random rng)
    {
        // box-muller transform for actual noise ~N(0,1)
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

        // volatility params (0.15% per tick + light mean reversion toward seed)
        const double volatility = 0.0015;
        var drift = (fair - instrument!.SeedPrice) * -0.0005;

        // occasional regime jumps (2% chance of liquidity sweep)
        var jump = 0.0;
        if (rng.NextDouble() < 0.02)
        {
            jump = (rng.NextDouble() - 0.5) * 8 * instrument.TickSize;
        }

        // multiplicative log-return delta
        var delta = (fair * volatility * randStdNormal) + drift + jump;
        var newFair = fair + (long)Math.Round(delta);

        // quantize strictly to tickSize grid
        var remainder = newFair % instrument.TickSize;
        if (remainder != 0)
        {
            newFair += (remainder < instrument.TickSize / 2)
                ? -remainder
                : (instrument.TickSize - remainder);
        }

        return Math.Max(instrument.TickSize, newFair);
    }

    private void TrimBook(SimulatedInstrument instrument, List<(long id, Side side)> resting, int cap = 40)
    {
        while (resting.Count > cap) // trim from the oldest
        {
            gateway.Cancel(resting[0].id, instrument.SecurityId, _config.Username);
            resting.RemoveAt(0);
        }
    }

    /// <summary>
    /// Generates exponentially distributed delays(Poisson arrival process)
    /// </summary>
    private static int Jitter(int meanMs, Random rng)
    {
        // -ln(1-U)*mean
        double u = rng.NextDouble();
        // prevent Math.Log(0)
        if (u >= 1.0)
            u = 0.99999999;

        double delay = -Math.Log(1 - u) * meanMs;
        return Math.Max(1, (int)delay);
    }

    private async Task RunInstrument(SimulatedInstrument instrument, CancellationToken token)
    {
        Random rng = new(unchecked((int)(instrument.SecurityId * 766564524)));
        long fair = instrument.SeedPrice;
        var resting = new List<(long id, Side side)>(); // ids I placed

        SeedBook(instrument, ref fair, rng, resting);

        var meanDelayMs = (int)(1000.0 / Math.Max(0.1, instrument.OrdersPerSecond));

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Jitter(meanDelayMs, rng), token);
                fair = RandomWalk(fair, instrument, rng);

                var roll = rng.NextDouble();
                if (roll < 0.55)
                    AddLiquidity(instrument, fair, rng, resting);
                else if (roll < 0.80)
                    PullLiquidity(instrument, rng, resting);
                else
                    Aggress(instrument, rng);
                TrimBook(instrument, resting);
            }
            catch (OperationCanceledException e)
            {
                break; // graceful shutdown
            }
            catch (Exception e)
            {
                logger.Info("MarketSimulator", $"tick error on {instrument.SecurityId}:  {e.Message}");
            }
        }
    }

    private void SeedBook(
        SimulatedInstrument instrument,
        ref long fair, // passes a reference to it so we can modify the original fair
        Random rng, List<(long, Side)> resting
    )
    {
        for (var lvl = 1; lvl <= instrument.BookDepth; lvl++)
        {
            var away = lvl * instrument.TickSize;
            var quantity = (uint)(rng.Next(2, 10) * 20);
            var bid = gateway.Submit(new PlaceOrderRequest(
                instrument.SecurityId,
                _config.Username,
                Side.Buy,
                fair - away,
                quantity
            ));

            var ask = gateway.Submit(new PlaceOrderRequest(
                instrument.SecurityId,
                _config.Username,
                Side.Sell,
                fair + away,
                quantity
            ));

            if (HasRestingRemainder(bid, Side.Buy, quantity))
                resting.Add((bid.OrderId, Side.Buy));

            if (HasRestingRemainder(ask, Side.Sell,  quantity))
                resting.Add((ask.OrderId, Side.Sell));
        }
    }

    private static uint FilledQuantityFor(
        OrderAck ack,
        Side side
    )
    {
        return (uint)ack.Fills
            .Where(
                fill => side == Side.Buy
                ? fill.BidOrderId == ack.OrderId
                : fill.AskOrderId == ack.OrderId)
            .Sum(fill => (long)fill.Quantity);
    }

    private static bool HasRestingRemainder(
        OrderAck ack,
        Side side,
        uint SubmittedQuantity
    )
    {
        return FilledQuantityFor(ack, side) < SubmittedQuantity;
    }
}