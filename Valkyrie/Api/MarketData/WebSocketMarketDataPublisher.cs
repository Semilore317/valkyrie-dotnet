using System.Text.Json;
using Valkyrie.Api.Dto;
using Valkyrie.MatchingEngine;

namespace Valkyrie.Api.MarketData;

public sealed class WebSocketMarketDataPublisher(MarketDataHub Hub) : IMarketDataPublisher
{
    // use camelCase
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    
    private static byte[] Serialize(Object o) => JsonSerializer.SerializeToUtf8Bytes(o, Json);
    
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
            trade.FilledAt
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
    
}