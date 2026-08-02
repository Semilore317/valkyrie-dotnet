namespace Valkyrie.Instrument.Configuration;

public class InstrumentConfiguration
{
    public long SecurityId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}