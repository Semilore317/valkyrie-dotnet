namespace Valkyrie.OrderBook;

public interface IReadonlyOrderBook
{
    bool ContainsOrder(long orderId);
    OrderBookSpread GetSpread();
    int Count { get; } // for clarity... this represents the number of orders in the orderbook
}