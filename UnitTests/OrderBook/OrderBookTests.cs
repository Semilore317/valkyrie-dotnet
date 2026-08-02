using Valkyrie.OrderBook;
using Valkyrie.Orders;
using FluentAssertions;
using Valkyrie.Instruments;

namespace UnitTests;

public class OrderBookTests
{
    private Security _security;
    private OrderBook _orderbook;

    public OrderBookTests()
    {
        _security = new Security(1, "MSFT", "Microsoft Corporation");
        _orderbook = new OrderBook(_security);
    }

    [Fact]
    public void AddOrder_ShouldInsertAndIndexCorrectly()
    {
        // Arrange
        var buyOrder = new Order(1, 1, "Jane Street", Side.Buy, 75, 100);
        var sellOrder = new Order(2, 2, "JPMORGAN CHASE", Side.Sell, 76, 100);

        // Act
        _orderbook.AddOrder(buyOrder);
        _orderbook.AddOrder(sellOrder);

        var expectedSpread = _orderbook.GetSpread();
        var orderbookSpread = new OrderBookSpread(buyOrder.Price, sellOrder.Price);

        // Assert
        _orderbook.Count.Should().Be(2);
        _orderbook.ContainsOrder(buyOrder.OrderId).Should().BeTrue();
        _orderbook.ContainsOrder(sellOrder.OrderId).Should().BeTrue();

        _orderbook.GetSpread().Should().BeEquivalentTo(expectedSpread);
    }

    /// <summary>
    /// essential to assert there are no memory leaks!!!
    /// </summary>
    [Fact]
    public void RemoveOrder_ShouldCleanUpAndPruneEmptyLimits()
    {
        var limit = new Limit(100);

        var order = new Order(1, 1, "HRT", Side.Buy, 75, 100);
        var entry = new OrderbookEntry(order, limit);

        var cancelOrder = new CancelOrder(order.OrderId, order.SecurityId, order.Username);

        // Act
        _orderbook.AddOrder(order);
        _orderbook.RemoveOrder(cancelOrder);

        // Assert
        _orderbook.Count.Should().Be(0);
        _orderbook.ContainsOrder(order.OrderId).Should().BeFalse();
        _orderbook.GetSpread().Ask.Should().BeNull();
        _orderbook.GetSpread().Bid.Should().BeNull();
        _orderbook.GetSpread().Spread.Should().BeNull();
    }

    [Fact]
    public void ChangeOrder_ShouldReplaceOriginalOrder()
    {
        // Arrange
        var limit = new Limit(100);

        var order = new Order(1, 2, "Goldman Sachs", Side.Buy, 75, 100);
        var entry = new OrderbookEntry(order, limit);

        var modifyOrder = new ModifyOrder(
            order.OrderId,
            order.SecurityId,
            order.Username,
            Side.Sell,
            order.Price,
            80);

        // Act
        _orderbook.AddOrder(order);
        _orderbook.ChangeOrder(modifyOrder);

        // Assert
        _orderbook.Count.Should().Be(1);
        _orderbook.ContainsOrder(order.OrderId).Should().BeTrue();

        var bidOrders = _orderbook.GetBidOrders();
        var askOrders = _orderbook.GetAskOrders();
        bidOrders.Should().BeEmpty();
        askOrders.Should().ContainSingle();
        askOrders[0].CurrentQuantity.Should().Be(80, "The order quantity should update to the correct price");
        askOrders[0].Side.Should().Be(Side.Sell);
    }

    [Fact]
    public void GetSpread_WhenBookIsEmpty_ShouldReturnEmptySpread()
    {
        _orderbook.GetSpread().Spread.Should().BeNull();
    }

    [Fact]
    public void GetSpread_WhenOnlyBidsOrAsks_ShouldReturnPartialSpread()
    {
        // Verify the spread returns null for the missing side and correctly resolves the populated side.
        var bidOrder = new Order(1, 1, "Citadel", Side.Buy, 100, 50);
        _orderbook.AddOrder(bidOrder);

        var spread = _orderbook.GetSpread();

        spread.Bid.Should().Be(100, "The Highest bid price should be reflected");
        spread.Ask.Should().BeNull("There are no asks in the order book");
        spread.Spread.Should().BeNull("Spread cannot be calculated without both bid and ask");
    }

    [Fact]
    public void GetAskOrders_ShouldReturnEntriesInCorrectOrder()
    {
        // arrange
        var cheapAsk = new Order(1, 1, "Goldman Sachs", Side.Sell, 70, 100);
        var midAsk = new Order(2, 1, "Goldman Sachs", Side.Sell, 75, 100);
        var expensiveAsk = new Order(3, 1, "Goldman Sachs", Side.Sell, 80, 100);

        _orderbook.AddOrder(cheapAsk);
        _orderbook.AddOrder(midAsk);
        _orderbook.AddOrder(expensiveAsk);

        var _askOrders = _orderbook.GetAskOrders();

        // assert
        _askOrders.Count.Should().Be(3);

        // should be sorted in ascending order
        _askOrders[0].Price.Should().Be(70);
        _askOrders[1].Price.Should().Be(75);
        _askOrders[2].Price.Should().Be(80);
    }

    [Fact]
    public void GetBidOrders_ShouldReturnEntriesInCorrectOrder()
    {
        // arrange
        var cheapBid = new Order(1, 1, "Jane Street", Side.Buy, 70, 100);
        var midBid = new Order(2, 1, "Jane Street", Side.Buy, 75, 100);
        var expensiveBid = new Order(3, 1, "Jane Street", Side.Buy, 80, 100);

        _orderbook.AddOrder(cheapBid);
        _orderbook.AddOrder(midBid);
        _orderbook.AddOrder(expensiveBid);

        var _bidOrders = _orderbook.GetBidOrders();

        // assert
        _orderbook.Count.Should().Be(3);

        // order book should be sorted in descending order
        _bidOrders[0].Price.Should().Be(80);
        _bidOrders[1].Price.Should().Be(75);
        _bidOrders[2].Price.Should().Be(70);
    }
}