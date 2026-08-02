namespace Valkyrie.Instruments;

public sealed class InstrumentCatalogue
{
    private readonly IReadOnlyDictionary<long, Security> _instrumentsBySecurityId;
    public IReadOnlyList<Security> Instruments { get; }

    public InstrumentCatalogue(IEnumerable<Security> instruments)
    {
        ArgumentNullException.ThrowIfNull(instruments, nameof(instruments));

        var instrumentsList = instruments.ToArray();

        if (instrumentsList.Length == 0)
            throw new InvalidOperationException("Instrument catalogue cannot be empty");

        var duplicateSecurityId = instrumentsList
            .GroupBy(instrument => instrument.SecurityId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSecurityId != null)
            throw new InvalidOperationException(
                $"Instrument catalogue contains duplicate security ID " +
                $"'{duplicateSecurityId.Key}'."
            );

        var duplicateTicker = instrumentsList
            .GroupBy(
                instrument => instrument.Ticker,
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateTicker != null)
            throw new InvalidOperationException(
                $"Instrument catalogue contains duplicate ticker " +
                $"'{duplicateTicker.Key}'.");

        Instruments = Array.AsReadOnly(instrumentsList);

        _instrumentsBySecurityId = instrumentsList.ToDictionary(
            instrument => instrument.SecurityId, instrument => instrument);
    }


    public Security Get(long securityId)
    {
        if (_instrumentsBySecurityId.TryGetValue(
                securityId,
                out var instrument))
            return instrument;

        throw new InvalidOperationException($"No instrument is configured for security ID " +
                                            $"'{securityId}'.");
    }
}