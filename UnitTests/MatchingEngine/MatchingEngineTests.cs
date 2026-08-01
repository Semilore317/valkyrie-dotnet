using FluentAssertions;
using Valkyrie.Instruments;
using Valkyrie.MatchingEngine.Algorithms;
using Valkyrie.Orders;
using Engine = Valkyrie.MatchingEngine.MatchingEngine;

namespace UnitTests.MatchingEngine;

public class MatchingEngineTests
{

    [Fact]
    public void PartiallyFilledIncoming_RestsRemainderAtRemainingQuantity()
    {
        var engine = new Engine(Fifo.Instance);

        engine.AddOrderBook(new Security(1, "AAPL", "Apple Inc."));

        engine.AddOrder(new Order(1, 1, "Maker", Side.Sell, 100, 300u));
        engine.AddOrder(new Order(2, 1, "Taker", Side.Buy, 100, 500u));

        // Hit the resting bid with a large sell... it must only  fill the 200 remainder, not the whole 500 units
        var result = engine.AddOrder(new Order(3, 1, "Seller", Side.Sell, 100, 500u));

        result.Fills.Sum(f => f.FilledQuantity).Should().Be(200);
    }
}