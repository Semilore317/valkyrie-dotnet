using Valkyrie.Orders;

namespace Valkyrie.Api.Executions;

/// <summary>
/// one trader-facing side of a matching-engine fill
///
/// a matching engine fill contains both the bid and ask order
/// this describes what the fill meant to one trading session
/// </summary>
public record ExecutionRecord(
    Guid ExecutionId,
    Guid MatchId,
    Guid SessionId,
    long SecurityId,
    long OrderId,
    Side Side,
    long Price,
    uint Quantity,
    DateTime ExecutedAt,
    LiquidityRole LiquidityRole
);