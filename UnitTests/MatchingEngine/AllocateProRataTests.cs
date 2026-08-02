using FluentAssertions;
using Valkyrie.MatchingEngine.Algorithms;
using Valkyrie.Orders;

namespace UnitTests.MatchingEngine;

// No constructor setup needed since AllocateProRata is a static method
// Build Limit + OrderbookEntry chains manually (same pattern as LimitTests.cs)
public class AllocateProRataTests
{
    /// <summary>
    /// Builds a limit with a chain of OrderbookEntry nodes
    /// with the order ... index 0 --> oldest (front of queue) = highest time priority.
    /// </summary>
    private static Limit BuildLevel(
        long price,
        // accepts a variable number of arguments of a tuple of long and uint instead of wrapping them in an array
        params (long orderId, uint quantity)[] orders
    )
    {
        var limit = new Limit(price);
        OrderbookEntry? previous = null;

        foreach (var (orderId, quantity) in orders)
        {
            var order = new Order(orderId, securityId: 1, username: "Belvedere", Side.Buy, price, quantity);
            var entry = new OrderbookEntry(order, limit);

            entry.Previous = previous;
            entry.Next = null;

            if (previous == null)
                limit.Head = entry;
            else
                previous.Next = entry;


            previous = entry;
        }

        limit.Tail = previous;
        return limit;
    }

    [Fact]
    public static void EqualSizedOrders_ShouldSplitEvenly()
    {
        // Arrange: Create a resting price level with a total depth of 400 units (100 + 200 + 100)
        var level = BuildLevel(100, (1, 100u), (2, 200u), (3, 100u));

        // ACT
        // ensure the entire incoming order is filled
        var result = ProRata.AllocateProRata(level, 150);

        // Assert: 
        result.Count.Should().Be(3);
        result.Sum(r => r.Allocated).Should().Be(150u);

        /*
         * Total depth = 400u, Incoming order = 150u
         *
         * Math:
         * order 1 (100u) --> 150*(100/400)  = 37.5 => 38
         */
        result.Select(r => r.Allocated).Should().Equal(38u, 75u, 37u);
    }

    [Fact]
    public static void ProportionalAllocation_ShouldReflectOrderSize()
    {
        var level = BuildLevel(100, (1, 100u), (2, 200u), (3, 100u));

        var result = ProRata.AllocateProRata(level, 400);

        result.Count.Should().Be(3);
        result.Sum(r => r.Allocated).Should().Be(400u);
        result.Single(r => r.Entry.OrderId == 1).Allocated.Should().Be(100u);
        result.Single(r => r.Entry.OrderId == 2).Allocated.Should().Be(200u);
        result.Single(r => r.Entry.OrderId == 3).Allocated.Should().Be(100u);
    }

    [Fact]
    public static void IncomingExceedsLevel_ShouldAllocateFullQuantity()
    {
        var level = BuildLevel(100, (1, 100u), (2, 150u));

        var result = ProRata.AllocateProRata(level, 1000);

        result.Single(r => r.Entry.OrderId == 1).Allocated.Should().Be(100u);
        result.Single(r => r.Entry.OrderId == 2).Allocated.Should().Be(150u);
    }

    [Fact]
    public static void RemainderDistribution_GoesToLargestFraction()
    {
        var level = BuildLevel(100, (1, 100u), (2, 100u), (3, 100u));

        var result = ProRata.AllocateProRata(level, 100);

        result.Single(r => r.Entry.OrderId == 1).Allocated.Should().Be(34u);
        result.Single(r => r.Entry.OrderId == 2).Allocated.Should().Be(33u);
        result.Single(r => r.Entry.OrderId == 3).Allocated.Should().Be(33u);
        result.Sum(r => r.Allocated).Should().Be(100u);
    }

    [Fact]
    public static void ZeroAllocation_OrdersExcluded()
    {
        // order 3 is small enough its floor share is 0 and it doesn't win a remainder lot
        var level = BuildLevel(100, (1, 970u), (2, 20u), (3, 10u));

        var result = ProRata.AllocateProRata(level, 10);

        result.Should().NotContain(r => r.Entry.OrderId == 3);
        result.Sum(r => r.Allocated).Should().Be(10u);
    }

    [Theory]
    [InlineData(100u, 100u)] // incoming == resting --> fill, fill
    [InlineData(50u, 100u)] // incoming < resting --> partial fill, fill
    public static void SingleOrder_GetsAllocation(uint incomingQuantity, uint restingQuantity)
    {
        var level = BuildLevel(100, (1, restingQuantity));

        var result = ProRata.AllocateProRata(level, incomingQuantity);

        result.Count.Should().Be(1);
        result[0].Allocated.Should().Be((Math.Min(incomingQuantity, restingQuantity)));
    }
}