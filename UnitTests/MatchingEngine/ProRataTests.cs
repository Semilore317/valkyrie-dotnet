using FluentAssertions;
using Valkyrie.Instruments;
using Valkyrie.MatchingEngine.Algorithms;
using Valkyrie.Orders;
using Engine = Valkyrie.MatchingEngine.MatchingEngine;

namespace UnitTests.MatchingEngine;

public class ProRataTests
{
    private readonly Security _security = new Security(1, "AAPL");
    private static readonly IComparer<Limit> BidComparer = BidLimitComparer.Comparer;
    private static readonly IComparer<Limit> AskComparer = AskLimitComparer.Comparer;

    public ProRataTests()
    {
        var engine = new Engine(ProRata.Instance);
        engine.AddOrderBook(_security);
    }

    private static OrderbookEntry MakeEntry(
        long orderId,
        long price,
        uint quantity,
        Side side,
        Limit parentLimit
    )
    {
        var order = new Order(orderId, securityId: 1, username: "Optiver", side, price, quantity);
        return new OrderbookEntry(order, parentLimit);
    }

    private static Limit BuildLevel(
        long price,
        Side side,
        Dictionary<long, OrderbookEntry> orders,
        params (long orderId, uint quantity)[] entries)
    {
        var limit = new Limit(price);
        OrderbookEntry? previous = null;

        foreach (var (orderId, quantity) in entries)
        {
            var entry = MakeEntry(orderId, price, quantity, side, limit);
            entry.Previous = previous;

            if (previous == null)
            {
                limit.Head = entry;
            }
            else
            {
                previous.Next = entry;
            }

            previous = entry;
            orders[orderId] = entry;
        }

        limit.Tail = previous;
        return limit;
    }

    [Fact]
    public static void NoMatch_WhenSpreadNotCrossed()
    {
        var orders = new Dictionary<long, OrderbookEntry>();
        var bidLimits = new SortedSet<Limit>(BidComparer);
        var askLimits = new SortedSet<Limit>(AskComparer);

        askLimits.Add(BuildLevel(100, Side.Sell, orders, (2, 100u)));

        var incoming = new Order(1,1 , "Belvedere", Side.Buy, 99, 100u);
        
        var result = ProRata.Instance.MatchIncoming(incoming,bidLimits, askLimits, orders);

        result.Fills.Should().BeEmpty();
        orders.Count.Should().Be(1);
    }

    [Fact]
    public static void BidIsAggressor_ProportionsFill_ProducesCorrectFills()
    {
        var orders = new Dictionary<long, OrderbookEntry>();
        var bidLimits = new SortedSet<Limit>(BidComparer);
        var askLimits = new SortedSet<Limit>(AskComparer);

        // aggressor --> 300 QTY
        var incomingBid = new Order(1, 1, "Jump Trading", Side.Buy, 100, 300u);
        // passive/resting: 600 total QTY splits across 3 orders
        askLimits.Add(BuildLevel(100, Side.Sell, orders, (2, 100u), (3, 200u), (4, 300u)));

        var result = ProRata.Instance.MatchIncoming(incomingBid, bidLimits, askLimits, orders);

        // The 300 incoming should split proportionally: 50, 100, 150
        result.Fills.Count.Should().Be(3);

        result.Fills.Single(f => f.AskOrderId == 2).FilledQuantity.Should().Be(50u);
        result.Fills.Single(f => f.AskOrderId == 3).FilledQuantity.Should().Be(100u);    
        result.Fills.Single(f => f.AskOrderId == 4).FilledQuantity.Should().Be(150u);
        
        // aggressor SHOULD be fully consumed, removed from dict
        orders.ContainsKey(1).Should().BeFalse();
        bidLimits.Should().BeEmpty();

        // passive SHOULD remain in the book with reduced quantities
        orders[2].CurrentQuantity.Should().Be(50u);
        orders[3].CurrentQuantity.Should().Be(100u);
        orders[4].CurrentQuantity.Should().Be(150u);
    }

    [Fact]
    public static void IncomingBuy_SplitsAcrossRestingAsksBySize()
    {
        var orders = new Dictionary<long, OrderbookEntry>();
        var bidLimits = new SortedSet<Limit>(BidComparer);
        var askLimits = new SortedSet<Limit>(AskComparer);
        
        askLimits.Add(BuildLevel(100, Side.Sell, orders, (2, 100u), (3, 200u), (4, 300u)));

        var incoming = new Order(1,1, "HRT", Side.Buy, 100, 300u);
        
        var result = ProRata.Instance.MatchIncoming(incoming, bidLimits, askLimits, orders);
        
        result.Fills.Single(f => f.AskOrderId == 2).FilledQuantity.Should().Be(50u);
        result.Fills.Single(f => f.AskOrderId == 3).FilledQuantity.Should().Be(100u);
        result.Fills.Single(f => f.AskOrderId == 4).FilledQuantity.Should().Be(150u);
        incoming.CurrentQuantity.Should().Be(0u);
    }
}
