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
        if (string.IsNullOrWhiteSpace(instrument.Symbol))
        {
            throw new InvalidDataException("Configured LOBSTER replay symbol cannot be empty");
        }

        var sessionDate = instrument.SessionMidnight.ToString(
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture
        );

        var expectedPrefix = $"{instrument.Symbol}_{sessionDate}_";

        if (datasetPrefix.StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return;
        }

        throw new InvalidDataException(
            $"LOBSTER dataset '{datasetPrefix}' does not match "+
            $"configured symbol '{instrument.Symbol}' and "+
            $"session date '{sessionDate}'."
        );
    }
}