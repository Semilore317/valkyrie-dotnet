using FluentAssertions;
using Valkyrie.Instrument;
using Valkyrie.MatchingEngine;
using Valkyrie.MatchingEngine.Algorithms;
using Valkyrie.Orders;

using Engine = Valkyrie.MatchingEngine.MatchingEngine;
using OrderBook = Valkyrie.OrderBook.OrderBook;

namespace UnitTests.MatchingEngine;

public class OrderBookSnapshotTests
{
    [Fact]
    public void Snapshot_AggregatesAsksAndBidsNull_WhenBookHasOnlyRestingAsks()
    {
        var security = new Security(1, "SPCX");
        var engine = new Engine(Fifo.Instance);
        engine.AddOrderBook(security);
        
        // add two  resting orders at the same price level to test aggregation
        engine.AddOrder(
            new Order(1, securityId: security.SecurityId, "JP Morgan", Side.Sell, price: 100, initialQuantity: 50u));
        engine.AddOrder(
            new Order(2, securityId: security.SecurityId, "Goldman Sachs", Side.Sell, price: 100, initialQuantity: 150u));
        
        // construct snapshot
        bool found = engine.TryGetSnapshot(
            security.SecurityId,out OrderBookSnapshot? snapshot); 
        
        
        // assert
        found.Should().BeTrue();
        snapshot!.Bid.Should().BeNull();
        snapshot.Ask.Should().Be(100);
        snapshot.Spread.Should().BeNull(); // no bid ==> no spread

        snapshot.Bids.Should().BeEmpty();
        snapshot.Asks.Should().ContainSingle().Which.Should().Be(new Level(100, 200)); // 50 + 150 aggregated
    }
}