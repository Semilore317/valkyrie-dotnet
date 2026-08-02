namespace Valkyrie.OrderBook;

// <summary>
// Combines retrieval (read) and order entry (write) permissions.
// This interface represents the full access required by the matching engine to process trades.
// </summary>
public interface IMatchingOrderBook : IRetrievalOrderBook
{
}