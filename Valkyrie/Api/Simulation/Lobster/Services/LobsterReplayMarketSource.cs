using Microsoft.Extensions.Options;
using Valkyrie.Api.Dto;
using Valkyrie.Api.MarketData;
using Valkyrie.Api.Simulation.Lobster.Models;
using Valkyrie.Core.Configuration;
using Valkyrie.Instruments;

namespace Valkyrie.Api.Simulation.Lobster.Services;

public sealed class LobsterReplayMarketSource(
    LobsterReplayReader reader,
    IMarketDataPublisher publisher,
    IOptions<MarketSimulatorConfiguration> options,
    IReplayDelay delay,
    InstrumentCatalogue instrumentCatalogue
) : IMarketDataSource
{
    private readonly HistoricalReplayConfiguration _configuration = options.Value.HistoricalReplay;
    public string Name => "LOBSTER Historical Replay";

    public async Task RunAsync(CancellationToken token)
    {
        ValidateConfiguration();

        var repeatedPass = false;

        try
        {
            do
            {
                token.ThrowIfCancellationRequested();

                var replayTasks = _configuration.Instruments
                    .Select(instrument => ReplayOnceAsync(instrument, repeatedPass, token))
                    .ToArray();

                await Task.WhenAll(replayTasks);

                repeatedPass = true;
            } while (_configuration.Loop && !token.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task ReplayOnceAsync(
        HistoricalReplayInstrument instrument,
        bool repeatedPass,
        CancellationToken token
    )
    {
        var minimumUpdateInterval =
            GetMinimumUpdateInterval();

        if (repeatedPass)
        {
            await delay.DelayAsync(
                minimumUpdateInterval,
                token
            );
        }

        LobsterReplayFrame? lastPublished = null;
        LobsterReplayFrame? pending = null;

        var pendingMarketTrades = new List<MarketTradeEvent>();

        await foreach (
            var frame in reader.ReadAsync(
                instrument,
                token
            )
        )
        {
            var marketTrade =
                LobsterMarketTradeMapper.Map(frame);

            if (marketTrade is not null)
                pendingMarketTrades.Add(marketTrade);

            pending = frame;

            if (lastPublished is null)
            {
                PublishPendingMarketTrades(
                    pendingMarketTrades
                );

                publisher.PublishBook(frame.Snapshot);

                lastPublished = frame;
                pending = null;

                continue;
            }

            var historicalDelay =
                frame.HistoricalTimeStamp -
                lastPublished.HistoricalTimeStamp;

            var scaledDelay = ScaleDelay(
                historicalDelay,
                _configuration.PlaybackSpeed
            );

            if (scaledDelay < minimumUpdateInterval)
                continue;

            await delay.DelayAsync(
                scaledDelay,
                token
            );

            PublishPendingMarketTrades(
                pendingMarketTrades
            );

            publisher.PublishBook(frame.Snapshot);

            lastPublished = frame;
            pending = null;
        }

        if (lastPublished is null)
        {
            throw new InvalidDataException(
                $"LOBSTER dataset for '{instrument.Ticker}' " +
                "does not contain any replay frames."
            );
        }

        if (pending is null)
            return;

        var finalHistoricalDelay =
            pending.HistoricalTimeStamp -
            lastPublished.HistoricalTimeStamp;

        var finalScaledDelay = ScaleDelay(
            finalHistoricalDelay,
            _configuration.PlaybackSpeed
        );

        var finalDelay =
            finalScaledDelay < minimumUpdateInterval
                ? minimumUpdateInterval
                : finalScaledDelay;

        await delay.DelayAsync(
            finalDelay,
            token
        );

        PublishPendingMarketTrades(
            pendingMarketTrades
        );

        publisher.PublishBook(pending.Snapshot);
    }

    private void PublishPendingMarketTrades(
        List<MarketTradeEvent> pendingMarketTrades
    )
    {
        foreach (var marketTrade in pendingMarketTrades)
        {
            publisher.PublishMarketTrade(
                marketTrade
            );
        }

        pendingMarketTrades.Clear();
    }

    private TimeSpan GetMinimumUpdateInterval()
    {
        var intervalTicks = (long)Math.Ceiling(
            (double)TimeSpan.TicksPerSecond / _configuration.MaxBookUpdatesPerSecond);

        return TimeSpan.FromTicks(Math.Max(1, intervalTicks));
    }

    private static TimeSpan ScaleDelay(
        TimeSpan historicalDelay,
        double playbackSpeed
    )
    {
        if (historicalDelay < TimeSpan.Zero)
            throw new InvalidDataException("Historical replay time moved backwards");

        var scaledTicks = historicalDelay.Ticks / playbackSpeed;

        return TimeSpan.FromTicks((long)Math.Round(scaledTicks, MidpointRounding.AwayFromZero));
    }

    private void ValidateConfiguration()
    {
        if (!double.IsFinite(_configuration.PlaybackSpeed) || _configuration.PlaybackSpeed <= 0)
            throw new InvalidOperationException("Historical Replay playback speed must be" +
                                                "finite and greater than zero");

        if (_configuration.MaxBookUpdatesPerSecond <= 0)
            throw new InvalidOperationException("Historical replay update rate must be greater than zero");

        if (_configuration.Instruments.Count == 0)
            throw new InvalidOperationException("Historical replay instruments must NOT be empty");

        var duplicateInstrument = _configuration.Instruments
            .GroupBy(instrument => instrument.SecurityId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateInstrument != null)
            throw new InvalidOperationException("Historical replay instruments must have unique security IDs. "
                                                + $"Duplicate security ID: '{duplicateInstrument.Key}'.");

        foreach (var replayInstrument in _configuration.Instruments)
        {
            if (string.IsNullOrWhiteSpace(replayInstrument.Ticker))
                throw new InvalidOperationException(
                    "Historical replay instrument ticker cannot be empty");

            var catalogueInstrument = instrumentCatalogue.Get(replayInstrument.SecurityId);

            if (!string.Equals(
                    replayInstrument.Ticker,
                    catalogueInstrument.Ticker,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Historical replay ticker `{replayInstrument.Ticker}` " +
                    $"for security ID `{replayInstrument.SecurityId}` does not match " +
                    $"catalogue ticker `{catalogueInstrument.Ticker}`.");
        }
    }
}
