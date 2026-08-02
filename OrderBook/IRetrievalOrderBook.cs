using Valkyrie.Orders;

namespace Valkyrie.OrderBook;
// <summary>
// Retrieval interface of the orderbook.
// Allows retrieving the full lists of active bid and ask orders.
// </summary>
public interface IRetrievalOrderBook : IOrderEntryOrderBook
{
    List<OrderbookEntry> GetAskOrders();
    List<OrderbookEntry> GetBidOrders();
}