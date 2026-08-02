using Valkyrie.Api.Dto;
using Valkyrie.MatchingEngine;

namespace Valkyrie.Api.MarketData;

public interface IMarketDataPublisher
{
    void PublishTrade(TradeEvent tradeEvent);
    void PublishBook(OrderBookSnapshot bookSnapshot);
    void PublishMarketTrade(MarketTradeEvent marketTradeEvent);
}