using FluentAssertions;
using Valkyrie.Orders;

namespace UnitTests;

/// <summary>
/// i'm simply checking for pointer linkage and limit references
/// </summary>
public class OrderBookEntryTests
{
    [Fact]
    public void OrderBookEntry_ShouldLinkToParentLimitAndNeighborNodes()
    {
        // Arrange
        var limit = new Limit(15000);
        var order_1 = new Order(1, 1, "Goldman Sachs", Side.Sell, 100, 20);
        var order_2 = new Order(2, 1, "Goldman Sachs", Side.Sell, 120, 30);

        //  Act
        var entry_1 = new OrderbookEntry(order_1, limit);
        var entry_2 = new OrderbookEntry(order_2, limit);

        entry_1.Next = entry_2;
        entry_2.Previous = entry_1;

        // Assert
        entry_1.ParentLimit.Should().Be(limit);
        entry_1.Next.Should().Be(entry_2);
        entry_2.Previous.Should().Be(entry_1);

    }
}