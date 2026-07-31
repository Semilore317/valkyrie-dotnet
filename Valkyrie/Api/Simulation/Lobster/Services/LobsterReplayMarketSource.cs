using Microsoft.Extensions.Options;
using Valkyrie.Api.MarketData;
using Valkyrie.Api.Simulation.Lobster.Models;
using Valkyrie.Core.Configuration;

namespace Valkyrie.Api.Simulation.Lobster.Services;

public sealed class LobsterReplayMarketSource(
    LobsterReplayReader reader,
    IMarketDataPublisher publisher,
    IOptions<MarketSimulatorConfiguration> options,
    IReplayDelay delay
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
        CancellationToken token)
    {
        var minimumUpdateInterval =
            GetMinimumUpdateInterval();

        if (repeatedPass)
        {
            await delay.DelayAsync(
                minimumUpdateInterval,
                token);
        }

        LobsterReplayFrame? lastPublished = null;
        LobsterReplayFrame? pending = null;

        await foreach (
            var frame in reader.ReadAsync(instrument, token))
        {
            pending = frame;

            if (lastPublished is null)
            {
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
                _configuration.PlaybackSpeed);

            if (scaledDelay < minimumUpdateInterval)
                continue;

            await delay.DelayAsync(
                scaledDelay,
                token);

            publisher.PublishBook(frame.Snapshot);

            lastPublished = frame;
            pending = null;
        }

        if (lastPublished is null)
        {
            throw new InvalidDataException(
                $"LOBSTER dataset for '{instrument.Symbol}' " +
                "does not contain any replay frames.");
        }

        if (pending is null)
            return;

        var finalHistoricalDelay =
            pending.HistoricalTimeStamp -
            lastPublished.HistoricalTimeStamp;

        var finalScaledDelay = ScaleDelay(
            finalHistoricalDelay,
            _configuration.PlaybackSpeed);

        var finalDelay =
            finalScaledDelay < minimumUpdateInterval
                ? minimumUpdateInterval
                : finalScaledDelay;

        await delay.DelayAsync(
            finalDelay,
            token);

        publisher.PublishBook(pending.Snapshot);
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
            throw new InvalidOperationException("Historical replay instruments must be unique"
                                                + $"security ID: {duplicateInstrument.Key}");
    }
}