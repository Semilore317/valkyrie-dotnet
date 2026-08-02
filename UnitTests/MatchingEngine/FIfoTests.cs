using FluentAssertions;
using Valkyrie.MatchingEngine.Algorithms;
using Valkyrie.Orders;

namespace UnitTests.MatchingEngine;

public class FIfoTests
{
    private static readonly IComparer<Limit> BidComparer = BidLimitComparer.Comparer;
    private static readonly IComparer<Limit> AskComparer = AskLimitComparer.Comparer;

    private static OrderbookEntry MakeEntry(
        long orderId,
        long price,
        uint quantity,
        Side side,
        Limit parentLimit
    )
    {
        var order = new Order(orderId, securityId: 1, username: "Jane Street", side, price, quantity);
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
    public void NoCross_NoFillsProduced()
    {
        var orders = new Dictionary<long, OrderbookEntry>();
        var bidLimits = new SortedSet<Limit>(BidComparer);
        var askLimits = new SortedSet<Limit>(AskComparer);

        askLimits.Add(BuildLevel(100, Side.Sell, orders, (2, 100u)));

        var incoming = new Order(1, 1, "Optiver", Side.Buy, 99, 100u);

        var result = Fifo.Instance.MatchIncoming(incoming, bidLimits, askLimits, orders);

        result.Fills.Should().BeEmpty();
        orders.Count.Should().Be(1);
    }

    [Fact]
    public void ExactMatch_FullyFillsBothSidesAndRemovesThem()
    {
        var orders = new Dictionary<long, OrderbookEntry>();
        var bidLimits = new SortedSet<Limit>(BidComparer);
        var askLimits = new SortedSet<Limit>(AskComparer);

        askLimits.Add(BuildLevel(99, Side.Sell, orders, (2, 100u)));

        var incoming = new Order(1, 1, "Jane Street", Side.Buy, 99, 100u);

        var result = Fifo.Instance.MatchIncoming(incoming, bidLimits, askLimits, orders);

        result.Fills.Count.Should().Be(1);

        var fill = result.Fills[0];

        fill.BidOrderId.Should().Be(1);
        fill.AskOrderId.Should().Be(2);
        fill.FilledQuantity.Should().Be(100u);
        fill.ExecutionPrice.Should().Be(99);

        orders.Should().BeEmpty();
        bidLimits.Should().BeEmpty();
        askLimits.Should().BeEmpty();
    }

    [Fact]
    public void CrossedBook_ExecutesAtRestingOrderPrice()
    {
        var orders = new Dictionary<long, OrderbookEntry>();
        var bidLimits = new SortedSet<Limit>(BidComparer);
        var askLimits = new SortedSet<Limit>(AskComparer);

        bidLimits.Add(BuildLevel(101, Side.Sell, orders, (1, 100u)));
        var incoming = new Order(2, 1, "JP Morgan", Side.Sell, 100, 100u);

        var result = ProRata.Instance.MatchIncoming(incoming, bidLimits, askLimits, orders);

        result.Fills.Count.Should().Be(1);
        var fill = result.Fills[0];
        fill.BidOrderId.Should().Be(1);         // resting buyer
        fill.AskOrderId.Should().Be(2);         // incoming seller
        fill.ExecutionPrice.Should().Be(101);   // resting price wins!!!
        fill.FilledQuantity.Should().Be(100);

    }
}