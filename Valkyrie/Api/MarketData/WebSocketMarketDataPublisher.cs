using System.Text.Json;
using Valkyrie.Api.Dto;
using Valkyrie.MatchingEngine;
using Valkyrie.Orders;

namespace Valkyrie.Api.MarketData;

public sealed class WebSocketMarketDataPublisher(MarketDataHub Hub) : IMarketDataPublisher
{
    // use camelCase
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    
    private static byte[] Serialize(Object value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);
    
    public void PublishTrade(TradeEvent trade)
    {
        Hub.BroadCast(trade.SecurityId, Serialize(new
        {
            type = "trade", 
            trade.SecurityId,
            trade.BidOrderId,
            trade.AskOrderId,
            trade.Price, 
            trade.Quantity, 
            trade.FilledAt,
            aggressorSide = ToWireSide(trade.AggressorSide)
        }));
    }

    public void PublishBook(OrderBookSnapshot bookSnapshot)
    {
        Hub.BroadCast(bookSnapshot.SecurityId, Serialize(new
        {
            type = "book", 
            bookSnapshot.SecurityId,
            bookSnapshot.Bid, 
            bookSnapshot.Ask, 
            bookSnapshot.Spread, 
            bookSnapshot.Bids,
            bookSnapshot.Asks
        }));
    }

    public void PublishMarketTrade(MarketTradeEvent trade)
    {
        Hub.BroadCast(trade.SecurityId, Serialize(new
        {
            type = "marketTrade",
            trade.SecurityId,
            trade.Price,
            trade.Quantity,
            aggressorSide = ToWireSide(trade.AggressorSide)
        }));
    }


    private static string ToWireSide(Side side)
    {
        return side switch
        {
            Side.Buy => "buy",
            Side.Sell => "sell",
            _ => throw new InvalidOperationException($"Trade aggressor side `{side}` is invalid")
        };
    }
}