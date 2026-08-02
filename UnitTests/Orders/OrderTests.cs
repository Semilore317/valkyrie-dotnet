using FluentAssertions;
using Valkyrie.Orders;

namespace UnitTests;

public class OrderTests
{
    [Fact]
    public void Constructor_ShouldMapIdentifiersCorrectly()
    {
        var order = new Order(1, 1, "Belvedere", Side.Buy, 10000, 500);

        order.InitialQuantity.Should().Be(500);
        order.CurrentQuantity.Should().Be(500);
    }

    [Theory]
    [InlineData(100, 400)]
    [InlineData(500, 0)]
    public void IncreaseDecreaseQuantity_ShouldUpdateCurrentQuantity(uint fillQuantity, uint expectedRemainingQuantity)
    {
        // arrange
        var order = new Order(1, 1, "Citadel Securities", Side.Buy, 10000, 500);

        // act
        order.DecrementQuantity(fillQuantity);

        // assert
        order.CurrentQuantity.Should().Be(expectedRemainingQuantity);
    }

    [Fact]
    public void DecreaseQuantity_BeyondRemaining_ShouldThrowArgumentOutOfRangeException()
    {
        // arrange
        var order = new Order(1, 1, "Optiver", Side.Buy, 10000, 500);

        // act
        var action = () => order.DecrementQuantity(501);

        // assert
        action.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*cannot be greater than*");
    }

}