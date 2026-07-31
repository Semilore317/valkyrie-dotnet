using Valkyrie.Orders;

namespace Valkyrie.Api.Dto;

/// <summary>
///  a trade observed in an external market-data feed
/// prices are expressed in cents, I'm using decimal since LOBSTER 
/// hidden executions can be at sub-cent prices
/// </summary>
public record MarketTradeEvent(
    long SecurityId,
    decimal Price,
    uint Quantity,
    DateTimeOffset OccurredAt,
    Side AggressorSide
);