using Valkyrie.MatchingEngine;

namespace Valkyrie.Api.Simulation.Lobster.Models;

// perhaps this record should be cleaned up a bit
public record LobsterReplayFrame(
    long LineNumber,
    DateTimeOffset HistoricalTimeStamp,
    LobsterMessage Message,
    OrderBookSnapshot Snapshot);