using FluentAssertions;
using Valkyrie.Orders;

namespace UnitTests;

public class LimitTests
{
    [Fact]
    public void GetLevelOrderCount_ShouldSumValuesCorrectly()
    {
        // Arrange: Create a Limit level and add several orders to it.
        // create Order objects and link them into Limit's Head/Tail.

        var limit = new Limit(1500);
        var order1 = new Order(1, 1, "HRT", Side.Buy, 1500, 100);
        var order2 = new Order(2, 2, "OPTIVER", Side.Buy, 1500, 1200);

        var entry1 = new OrderbookEntry(order1, limit);
        var entry2 = new OrderbookEntry(order2, limit);

        entry1.Next = entry2;
        entry2.Previous = entry1;

        limit.Head = entry1;
        limit.Tail = entry2;

        // Act

        uint orderCount = limit.GetLevelOrderCount();

        // 3. Assert: Verify the sum matches the expected aggregate quantity. 
        2.Should().Be((int)orderCount);
    }

    [Fact]
    public void GetLevelOrderQuantity_ShouldSumValuesCorrectly()
    {
        // Assert
        var limit = new Limit(1500);

        var order1 = new Order(1, 1, "CITADEL SECURITIES", Side.Buy, 1500, 100);
        var order2 = new Order(2, 2, "JANE STREET", Side.Buy, 1500, 1200);

        var entry1 = new OrderbookEntry(order1, limit);
        var entry2 = new OrderbookEntry(order2, limit);

        entry1.Next = entry2;
        entry2.Previous = entry1;

        limit.Head = entry1;
        limit.Tail = entry2;

        // Act
        uint orderQuantity = limit.GetLevelOrderQuantity();

        // Assert
        orderQuantity.Should().Be(1300u);
    }

    /// <summary>
    /// This test verifies that the order book correctly extract the
    /// L3 snapshot records for external systems, specifically tracking their positions in queue.
    /// </summary>
    [Fact]
    public void GetLevelOrderRecords_ShouldReturnCorrectRecords()
    {
        // Arrange
        var limit = new Limit(1500);
        var order1 = new Order(1, 1, "JUMP TRADING", Side.Buy, 1500, 100);
        var order2 = new Order(2, 2, "TWO SIGMA", Side.Buy, 1500, 1200);

        var entry1 = new OrderbookEntry(order1, limit);
        var entry2 = new OrderbookEntry(order2, limit);

        entry1.Next = entry2;
        entry2.Previous = entry1;

        limit.Head = entry1;
        limit.Tail = entry2;

        // Act
        var records = limit.GetLevelOrderRecords();

        // Assert

        // count check
        records.Count.Should().Be(2);

        // position checks
        records[0].TheoreticalQueuePosition.Should().Be(0u);
        records[1].TheoreticalQueuePosition.Should().Be(1u);

        //property checks
        records[0].OrderId.Should().Be(1u);
        records[1].OrderId.Should().Be(2u);

        records[0].Quantity.Should().Be(100u);
        records[1].Quantity.Should().Be(1200u);

        records[0].Username.Should().Be("JUMP TRADING");
        records[1].Username.Should().Be("TWO SIGMA");
    }

    /// <summary>
    /// asserts that a brand new limit is empty
    /// </summary>
    [Fact]
    public void IsEmpty_WhenNoOrders_ShouldBeTrue()
    {
        var limit = new Limit(1500);
        limit.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WhenOrdersAddedAndRemoved_ShouldToggle()
    {
        // Verify the boolean accurately toggles as entries are injected into the Head/Tail.
        var limit = new Limit(1500);
        var order = new Order(1, 1, "JUMP TRADING", Side.Buy, 1500, 100);
        var entry = new OrderbookEntry(order, limit);

        limit.Head = entry;
        limit.Tail = entry;

        limit.IsEmpty.Should().BeFalse();

        limit.Head = null;
        limit.Tail = null;

        limit.IsEmpty.Should().BeTrue();
    }
}