using Valkyrie.MatchingEngine;
using Valkyrie.Orders;

namespace Valkyrie.Api.Dto;

public record TradeEvent(
    long SecurityId,
    long BidOrderId,
    long AskOrderId,
    long Price,
    uint Quantity,
    DateTime FilledAt,
    Side AggressorSide
)
{
    public static TradeEvent From(Fill fill, Side aggressorSide)
    {
        return new TradeEvent(
            fill.SecurityId,
            fill.BidOrderId,
            fill.AskOrderId,
            fill.ExecutionPrice,
            fill.FilledQuantity,
            fill.FilledAt,
            aggressorSide);
    }
}