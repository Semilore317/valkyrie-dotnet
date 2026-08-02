namespace Valkyrie.Instruments;

public class Security(
    long securityId,
    string ticker,
    string name
)
{
    public long SecurityId { get; } = securityId > 0
        ? securityId
        : throw new ArgumentOutOfRangeException(nameof(securityId));
    public string Ticker { get; } = string.IsNullOrWhiteSpace(ticker)
        ? throw new ArgumentOutOfRangeException(nameof(ticker))
        : ticker;
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentOutOfRangeException(nameof(name))
        : name;
}