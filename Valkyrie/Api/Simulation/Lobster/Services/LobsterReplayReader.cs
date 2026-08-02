using System.Runtime.CompilerServices;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Api.Simulation.Lobster.Input;
using Valkyrie.Api.Simulation.Lobster.Models;
using Valkyrie.Core.Configuration;

namespace Valkyrie.Api.Simulation.Lobster.Services;

public sealed class LobsterReplayReader
{
    private readonly IReadOnlyDictionary<LobsterDataFormat, ILobsterInputProvider> _providers;

    public LobsterReplayReader(
        IEnumerable<ILobsterInputProvider> providers
    )
    {
        ArgumentNullException.ThrowIfNull(providers);

        var providerDictionary = new Dictionary<LobsterDataFormat, ILobsterInputProvider>();

        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (!providerDictionary.TryAdd(provider.Format, provider))
                throw new InvalidDataException($"Multiple LOBSTER input providers are " +
                                               $"registered for format '{provider.Format}'.");
        }

        _providers = providerDictionary;
    }

    public async IAsyncEnumerable<LobsterReplayFrame> ReadAsync
    (
        HistoricalReplayInstrument instrument,
        [EnumeratorCancellation] CancellationToken token = default
    )
    {
        ArgumentNullException.ThrowIfNull(instrument);

        ValidateInstrument(instrument);

        if (!_providers.TryGetValue(instrument.DataFormat, out var provider))
        {
            throw new InvalidOperationException($"NO LOBSTER input provider is registered " +
                                                $"for {instrument.DataFormat}");
        }

        token.ThrowIfCancellationRequested();

        using var input = provider.Open(instrument);

        long lineNumber = 0;
        decimal? previousSeconds = null;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            var messageLine = await input.MessageReader.ReadLineAsync(token);
            var orderBookLine = await input.OrderBookReader.ReadLineAsync(token);

            if (messageLine is null && orderBookLine is null)
                yield break; // completely exits the method

            lineNumber++;

            if (messageLine is null)
                throw new InvalidDataException($"Message file ended before the order book file at row {lineNumber}.");

            if (orderBookLine is null)
                throw new InvalidDataException($"Order book file ended before the message file at row {lineNumber}.");

            var message = LobsterMessage.Parse(messageLine, lineNumber);

            ValidateTimeStamp(message.SecondsAfterMidnight, previousSeconds, lineNumber);

            previousSeconds = message.SecondsAfterMidnight;

            var snapshot  = LobsterOrderBookParser.Parse(orderBookLine, lineNumber, instrument.SecurityId, instrument.BookDepth);
            yield return new LobsterReplayFrame(
                lineNumber,
                ToHistoricalTimestamp(instrument.SessionMidnight, message.SecondsAfterMidnight),
                message,
                snapshot);
        }
    }

    private static void ValidateTimeStamp(decimal seconds, decimal? previousSeconds, long lineNumber)
    {
        if (seconds is < 0m or >= 86_400m)
            throw new InvalidDataException(
                $"Message row {lineNumber} has " +
                $"seconds after midnight:  '{seconds}'." +
                $"outside [0, 86400).");

        if (previousSeconds.HasValue && seconds < previousSeconds)
            throw new InvalidDataException($"Message Timestamp moved backwards at row {lineNumber}");
    }

    private static DateTimeOffset ToHistoricalTimestamp(
        DateTimeOffset sessionMidnight,
        decimal secondsAfterMidnight
    )
    {
        var ticks = decimal.ToInt64(decimal.Round(
            secondsAfterMidnight * TimeSpan.TicksPerSecond,
            decimals: 0,
            MidpointRounding.ToEven));
        return sessionMidnight.AddTicks(ticks);
    }

    private static void ValidateInstrument(HistoricalReplayInstrument instrument)
    {
        if (instrument.SecurityId <= 0)
            throw new InvalidDataException($"Invalid security id '{instrument.SecurityId}'.");

        if (instrument.BookDepth <= 0)
            throw new InvalidDataException($"Invalid book depth '{instrument.BookDepth}'.");

        if (instrument.SessionMidnight == default)
            throw new InvalidDataException($"Replay session midnight is required '{instrument.SessionMidnight}'.");

        if (instrument.SessionMidnight.TimeOfDay != TimeSpan.Zero)
            throw new InvalidOperationException("Replay session must  represent midnight");
    }
}
