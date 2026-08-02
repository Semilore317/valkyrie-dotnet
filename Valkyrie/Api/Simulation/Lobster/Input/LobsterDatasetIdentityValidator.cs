using System.Globalization;
using Valkyrie.Core.Configuration;

namespace Valkyrie.Api.Simulation.Lobster.Input;

internal class LobsterDatasetIdentityValidator
{
    public static void Validate(
        HistoricalReplayInstrument instrument,
        string datasetPrefix
    )
    {
        ArgumentNullException.ThrowIfNull(instrument);
        if (string.IsNullOrWhiteSpace(instrument.Ticker))
            throw new InvalidDataException("Configured LOBSTER replay Ticker cannot be empty");

        var sessionDate = instrument.SessionMidnight.ToString(
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture
        );

        var expectedPrefix = $"{instrument.Ticker}_{sessionDate}_";

        if (datasetPrefix.StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase
            ))
            return;

        throw new InvalidDataException(
            $"LOBSTER dataset '{datasetPrefix}' does not match "+
            $"configured ticker '{instrument.Ticker}' and "+
            $"session date '{sessionDate}'."
        );
    }
}