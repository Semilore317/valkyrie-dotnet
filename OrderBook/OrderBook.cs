using System.Runtime.CompilerServices;
using Valkyrie.Instrument;
using Valkyrie.Instruments;
using Valkyrie.Orders;

// grants private fields access without compromising encapsulation
[assembly: InternalsVisibleTo("MatchingEngine")] 
namespace Valkyrie.OrderBook;

public class OrderBook : IRetrievalOrderBook
{
    private readonly Security _instrument;
    
    // orders keyed by order-id
    private readonly Dictionary<long, OrderbookEntry> _orders = new Dictionary<long, OrderbookEntry>();
    
    // SortedSets of Limit price levels
    private readonly SortedSet<Limit> _askLimits = new SortedSet<Limit>(AskLimitComparer.Comparer);
    private readonly SortedSet<Limit> _bidLimits = new SortedSet<Limit>(BidLimitComparer.Comparer);

    // exposed as internal so only trusted assemblies (MatchingEngine ...for now) can access them
    internal SortedSet<Limit> AskLimits => _askLimits;
    internal SortedSet<Limit> BidLimits => _bidLimits;
    internal Dictionary<long, OrderbookEntry> Orders => _orders;

    public OrderBook(Security instrument)
    {
        _instrument = instrument;
    }
    
    public bool ContainsOrder(long orderId)
    {
        return _orders.ContainsKey(orderId);
    }

    public OrderBookSpread GetSpread()
    {
        long? bestAsk = null;
        long? bestBid = null;
        
        // AskLimits are sorted ascending (Min is the best ask price level)
        if (_askLimits.Any() && _askLimits.Min != null && !_askLimits.Min.IsEmpty)
        {
            bestAsk = _askLimits.Min.Price;
        }

        // BidLimits are sorted descending (Min is the best bid price level under BidLimitComparer)
        if (_bidLimits.Any() && _bidLimits.Min != null && !_bidLimits.Min.IsEmpty)
        {
            bestBid = _bidLimits.Min.Price; // FIXED: Use Min instead of Max
        }

        return new OrderBookSpread(bestBid, bestAsk);
    }

    public int Count  => _orders.Count;

    public List<OrderbookEntry> GetAskOrders()
    {
        List<OrderbookEntry> orderbookEntries = new List<OrderbookEntry>();
        foreach (var askLimit in _askLimits)
        {
            if (askLimit.IsEmpty)
                continue;
            OrderbookEntry? askLimitPointer = askLimit.Head; // FIXED: Mark askLimitPointer as nullable
            while (askLimitPointer != null)
            {
                orderbookEntries.Add(askLimitPointer);
                askLimitPointer = askLimitPointer.Next;
            }
        }

        return orderbookEntries;
    }

    public List<OrderbookEntry> GetBidOrders()
    {
        List<OrderbookEntry> orderbookEntries = new List<OrderbookEntry>();
        foreach (var bidLimit in _bidLimits)
        {
            if (bidLimit.IsEmpty)
                continue;
            OrderbookEntry? bidLimitPointer = bidLimit.Head; // FIXED: Mark pointer as nullable and rename for clarity
            while (bidLimitPointer != null)
            {
                orderbookEntries.Add(bidLimitPointer);
                bidLimitPointer = bidLimitPointer.Next;
            }
        }

        return orderbookEntries;
    }

    public void AddOrder(Order order)
    {
        var baseLimit = new Limit(order.Price);
        AddOrder(
            order, 
            baseLimit,
            order.IsBuySide ? _bidLimits : _askLimits,
            _orders
            );
    }

    private static void AddOrder(
        Order order,
        Limit baseLimit,
        SortedSet<Limit> limitLevels,
        Dictionary<long, OrderbookEntry> internalOrderBook)
    {
        if (limitLevels.TryGetValue(baseLimit, out Limit? limit))
        {
            // FIXED: Associate the entry with the active 'limit' level in the set, not the dummy baseLimit
            OrderbookEntry orderbookEntry = new OrderbookEntry(order, limit);
            if (limit.Head == null)
            {
                limit.Head = orderbookEntry; 
                limit.Tail = orderbookEntry;
            }
            else
            {
                OrderbookEntry? tailPointer = limit.Tail;
                if (tailPointer != null)
                {
                    tailPointer.Next = orderbookEntry;
                    orderbookEntry.Previous = tailPointer;
                }
                limit.Tail = orderbookEntry;
            }
            internalOrderBook.Add(order.OrderId, orderbookEntry);
        }
        else
        {
            limitLevels.Add(baseLimit);
            // FIXED: Associate the entry with 'baseLimit' (which is now in the set), not the null 'limit'
            OrderbookEntry orderbookEntry = new OrderbookEntry(order, baseLimit);
            
            baseLimit.Head = orderbookEntry;
            baseLimit.Tail = orderbookEntry;
            
            internalOrderBook.Add(order.OrderId, orderbookEntry);
        } 
    }

    public void ChangeOrder(ModifyOrder modifyOrder)
    {
        if (_orders.TryGetValue(modifyOrder.OrderId, out OrderbookEntry? orderbookEntry)) 
        {
           RemoveOrder(modifyOrder.ToCancelOrder());
           AddOrder(modifyOrder.ToNewOrder());
        }
    }

    public void RemoveOrder(CancelOrder cancelOrder)
    {
        if (_orders.TryGetValue(cancelOrder.OrderId, out OrderbookEntry? orderbookEntry))
        {
            RemoveOrder(cancelOrder.OrderId, orderbookEntry, _orders);
        }
    }

    // FIXED: Changed helper to non-static instance method to clean up empty limits from _bidLimits / _askLimits
    private void RemoveOrder(
        long orderId,
        OrderbookEntry orderbookEntry,
        Dictionary<long, OrderbookEntry> internalBook
    )
    {
        // Adjust linked-list pointers for the entries
        if (orderbookEntry.Previous != null && orderbookEntry.Next != null)
        {
            orderbookEntry.Next.Previous = orderbookEntry.Previous;
            orderbookEntry.Previous.Next = orderbookEntry.Next;
        }
        else if (orderbookEntry.Previous != null)
        {
            orderbookEntry.Previous.Next = null;
        }
        else if (orderbookEntry.Next != null)
        {
            orderbookEntry.Next.Previous = null;
        }
        
        // Adjust Head / Tail pointers of the containing Limit level
        if (orderbookEntry.ParentLimit.Head == orderbookEntry && orderbookEntry.ParentLimit.Tail == orderbookEntry)
        {
            orderbookEntry.ParentLimit.Head = null;
            orderbookEntry.ParentLimit.Tail = null;
        }
        else if (orderbookEntry.ParentLimit.Head == orderbookEntry)
        {
            orderbookEntry.ParentLimit.Head = orderbookEntry.Next;
        }
        else if (orderbookEntry.ParentLimit.Tail == orderbookEntry)
        {
            orderbookEntry.ParentLimit.Tail = orderbookEntry.Previous;
        }

        // FIXED: Remove empty price levels from SortedSet to prevent memory leaks
        if (orderbookEntry.ParentLimit.Head == null && orderbookEntry.ParentLimit.Tail == null)
        {
            if (orderbookEntry.IsBuySide)
                _bidLimits.Remove(orderbookEntry.ParentLimit);
            else
                _askLimits.Remove(orderbookEntry.ParentLimit);
        }

        internalBook.Remove(orderId);
    }
}