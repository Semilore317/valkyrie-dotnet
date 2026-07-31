using Valkyrie.Api.Dto;
using Valkyrie.Api.Simulation.Lobster.Models;
using Valkyrie.Orders;

namespace Valkyrie.Api.Simulation.Lobster.Services;

public static class LobsterMarketTradeMapper
{
    public static MarketTradeEvent? Map(LobsterReplayFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (!frame.Message.IsExecution)
            return null;

        var aggressorSide = frame.Message.Direction switch
        {
            LobsterDirection.Buy => Side.Sell,
            LobsterDirection.Sell => Side.Buy,
            _ => throw new InvalidDataException($"LOBSTER execution at row" +
                                                $"{frame.LineNumber} has an invalid direction")
        };

        return new MarketTradeEvent(
            SecurityId: frame.Snapshot.SecurityId,
            Price: frame.Message.PriceInCents,
            Quantity: frame.Message.Size,
            OccurredAt: frame.HistoricalTimeStamp,
            AggressorSide: aggressorSide
        );
    }
}