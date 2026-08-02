namespace Valkyrie.Api.Simulation.Lobster.Models;

public enum LobsterEventType
{
    Submission = 1,
    PartialCancellation = 2,
    Deletion = 3,
    VisibleExecution = 4,
    HiddenExecution = 5,
    CrossTrade = 6,
    TradingHalt = 7
}