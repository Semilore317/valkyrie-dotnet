using Valkyrie.Orders;

namespace Valkyrie.MatchingEngine.Algorithms;

/// <summary>
/// Pro-Rata Matching Algorithm
/// At each price level, it gives preference to the size of the order
/// so, an order with a larger share of the total volume gets preferential treatment
///
/// At each crossed price level, the side with the smaller aggregate quantity is considered the aggressor
/// and consumed in time-priority... the opposite side's resting orders are filled proportionally to their size
/// (Pro-Rata), with any rounding remainder  distributed by largest-remainder, tie-broken by time-priority(FIFO)
/// </summary>
public class ProRata : IMatchingAlgorithm
{
    public static readonly IMatchingAlgorithm Instance = new ProRata();

    private ProRata()
    {
    } // enforces singleton

    /// <summary>
    /// allocates incoming quantity across a level's resting orders proportionally to size.
    /// if incoming quantity consumes a whole level, every order is filled.
    /// otherwise each order gets floor(share), and leftover lots(from rounding down)
    /// are handed out one at a time by largest fractional remainder, tie-broken down by time priority(FIFO)
    /// </summary>
    /// <param name="level"></param>
    /// <param name="incomingQuantity"></param>
    /// <returns name=""></returns>
    public static List<(OrderbookEntry Entry, uint Allocated)> AllocateProRata(Limit level, uint incomingQuantity)
    {
        List<OrderbookEntry> entries = new List<OrderbookEntry>();
        OrderbookEntry? current = level.Head;

        while (current != null)
        {
            entries.Add(current);
            current = current.Next;
        }

        uint totalQuantity = 0;

        foreach (OrderbookEntry e in entries)
            totalQuantity += e.CurrentQuantity;

        if (incomingQuantity >= totalQuantity)
            return entries.Select(e => (e, e.CurrentQuantity)).ToList();

        uint[] alloc = new uint[entries.Count];
        double[] remainder = new double[entries.Count];
        uint allocatedSoFar = 0;


        for (int i = 0; i < entries.Count; i++)
        {
            double share = incomingQuantity * ((double)entries[i].CurrentQuantity / totalQuantity);
            uint floorShare = (uint)Math.Floor(share);
            alloc[i] = floorShare;
            remainder[i] = share - floorShare;
            allocatedSoFar += floorShare;
        }

        uint leftover = incomingQuantity - allocatedSoFar;

        int[] byRemainderThenTime = Enumerable.Range(0, entries.Count)
            .OrderByDescending(i => remainder[i])
            .ThenBy(i => i) // earlier index --> higher time priority
            .ToArray();

        for (int i = 0; i < leftover; i++)
            alloc[byRemainderThenTime[i]]++;

        List<(OrderbookEntry, uint)> result = new List<(OrderbookEntry, uint)>();

        for (int i = 0; i < entries.Count; i++)
        {
            if (alloc[i] > 0)
                result.Add((entries[i], alloc[i]));
        }

        return result;
    }

    public static void RemoveFilledOrder(
        OrderbookEntry entry,
        SortedSet<Limit> limitLevels,
        Dictionary<long, OrderbookEntry> orders
    )
    {
        Limit parentLimit = entry.ParentLimit;

        if (entry.Previous != null)
            entry.Previous.Next = entry.Next;
        if (entry.Next != null)
            entry.Next.Previous = entry.Previous;

        if (parentLimit.Head == entry)
            parentLimit.Head = entry.Next;
        if (parentLimit.Tail == entry)
            parentLimit.Tail = entry.Previous;

        orders.Remove(entry.OrderId);

        if (parentLimit.Head == null)
            limitLevels.Remove(parentLimit);
    }

    public MatchResult MatchIncoming(
        Order incoming,
        SortedSet<Limit> bidLimits,
        SortedSet<Limit> askLimits,
        Dictionary<long, OrderbookEntry> orders
    )
    {
        List<Fill> fills = new List<Fill>();

        SortedSet<Limit> restingLimits = incoming.IsBuySide ? askLimits : bidLimits;

        while (incoming.CurrentQuantity > 0 && restingLimits.Count > 0)
        {
            Limit bestLevel = restingLimits.Min!;

            bool crosses = incoming.IsBuySide
                ? incoming.Price >= bestLevel.Price
                : incoming.Price <= bestLevel.Price;

            if (!crosses)
                break;

            long executionPrice = bestLevel.Price;

            // allocate the incoming quantity across this level's resting order, by size
            List<(OrderbookEntry Entry, uint Allocated)> allocations =
                AllocateProRata(bestLevel, incoming.CurrentQuantity);

            foreach (var (restingEntry, allocated) in allocations)
            {
                uint tradeQuantity = Math.Min(allocated, incoming.CurrentQuantity);
                if (tradeQuantity == 0)
                    continue;

                incoming.DecrementQuantity(tradeQuantity);
                restingEntry.DecrementQuantity(tradeQuantity);

                fills.Add(new Fill
                {
                    SecurityId = incoming.SecurityId,
                    BidOrderId = incoming.IsBuySide ? incoming.OrderId : restingEntry.OrderId,
                    AskOrderId = incoming.IsBuySide ? restingEntry.OrderId : incoming.OrderId,
                    ExecutionPrice = executionPrice,
                    FilledQuantity = tradeQuantity,
                    FilledAt = DateTime.UtcNow
                });

                if (restingEntry.CurrentQuantity == 0)
                    RemoveFilledOrder(restingEntry, restingLimits, orders);
            }
        }
        return new MatchResult(fills);
    }
}
