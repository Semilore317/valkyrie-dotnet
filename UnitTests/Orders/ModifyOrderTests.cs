using FluentAssertions;
using Valkyrie.Orders;

namespace UnitTests;

/// <summary>
/// ModifyOrder acts as a payload that generates cancellation and replenishment orders.
/// The tests assert that the generated orders copy identity fields: OrderId, SecurityId and Username perfectly
/// </summary>
public class ModifyOrderTests
{

    [Fact]
    public void ToCancelOrder_ShouldMapPropertiesCorrectly()
    {
        // arrange
        var modifyOrder = new ModifyOrder(99, 1, "Optiver", Side.Buy, 200, 500);

        // act
        var cancelOrder = modifyOrder.ToCancelOrder();

        // assert
        cancelOrder.OrderId.Should().Be(modifyOrder.OrderId);
        cancelOrder.SecurityId.Should().Be(modifyOrder.SecurityId);
        cancelOrder.Username.Should().Be(modifyOrder.Username);
    }

    [Fact]
    public void ToNewOrder_ShouldCreateOrderWithModifiedValuesCorrectly()
    {
        // arrange
        var modifyOrder = new ModifyOrder(67, 89, "Jump Trading", Side.Buy, 200, 250);

        // act
        var newOrder = modifyOrder.ToNewOrder();

        // assert
        newOrder.OrderId.Should().Be(modifyOrder.OrderId);
        newOrder.SecurityId.Should().Be(modifyOrder.SecurityId);
        newOrder.Username.Should().Be(modifyOrder.Username);
        newOrder.Side.Should().Be(modifyOrder.Side);
        newOrder.Price.Should().Be(modifyOrder.Price);
        newOrder.InitialQuantity.Should().Be(modifyOrder.Quantity);
    }
}