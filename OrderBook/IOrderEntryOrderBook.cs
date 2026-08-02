using Valkyrie.Orders;

namespace Valkyrie.OrderBook;

// write only interface
public interface IOrderEntryOrderBook : IReadonlyOrderBook
{
    void AddOrder(Order order);
    void ChangeOrder(ModifyOrder modifyOrder);
    void RemoveOrder(CancelOrder cancelOrder);
}