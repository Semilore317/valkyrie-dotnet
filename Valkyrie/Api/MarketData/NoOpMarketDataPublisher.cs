using Valkyrie.Api.Dto;
using Valkyrie.MatchingEngine;

namespace Valkyrie.Api.MarketData;

/// <summary>
/// No-Operation stub implementation so the project compiles and runs before socket code exists
/// </summary>
public class NoOpMarketDataPublisher: IMarketDataPublisher
{
    
    public void PublishTrade(TradeEvent tradeEvent)
    {}

    public void PublishBook(OrderBookSnapshot bookSnapshot)
    {}

    public void PublishMarketTrade(MarketTradeEvent marketTradeEvent)
    {}
}