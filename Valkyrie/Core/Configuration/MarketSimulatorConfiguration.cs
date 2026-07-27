namespace Valkyrie.Core.Configuration;

public sealed class MarketSimulatorConfiguration
{
    public bool Enabled { get; set; }
    public string Username { get; set; } = "mm"; // market-maker identity
    public List<SimulatedInstrument> Instruments { get; set; } = new();
    
    public MarketDataSourceType Source { get; set; } = MarketDataSourceType.Synthetic;
    public HistoricalReplayConfiguration HistoricalReplay { get; set; } = new();
}

public enum MarketDataSourceType
{
    Synthetic,
    LobsterReplay
}

public sealed partial class HistoricalReplayConfiguration
{
    public double PlaybackSpeed { get; set; } = 1;
    public int MaxBookUpdatesPerSecond { get; set; } = 5;
    public bool Loop { get; set; }

    public List<HistoricalReplayInstrument> Instruments { get; set; } = [];
}

public sealed partial class HistoricalReplayInstrument
{
    public long SecurityId { get; set; }
    public string Symbol { get; set; } = string.Empty;

    // absolute or relative to the server content root
    public string ArchivePath { get; set; } = string.Empty;
}


public sealed class SimulatedInstrument
{
    public long SecurityId { get; set; }
    public long SeedPrice { get; set; }
    public long TickSize { get; set; } = 1; // $0.01
    public double OrdersPerSecond { get; set; } = 2.0;
    public int BookDepth { get; set; } = 6; // pending bids and asks on both sides i'm maintaining for the sim 
}
