namespace Valkyrie.Orders;

public class Limit
{

    // TODO: refactor this into a struct at some point for better performance

    /*
    this is essentially the maximum they're willing to pay (bid)
    or the minimum they're willing to accept (ask)
    if a trader wants to buy Microsoft stock MSFT at $100.00, Limit == $100.00
    */

    public Limit(long price)
    {
        Price = price;
    }

    public long Price { get; }
    public OrderbookEntry? Head { get; set; }
    public OrderbookEntry? Tail { get; set; }

    public bool IsEmpty => Head == null;

    public uint GetLevelOrderCount()
    {
        uint orderCount = 0;
        if (IsEmpty)
            return orderCount;

        OrderbookEntry? entry = Head;
        while (entry != null)
        {
            if (entry.CurrentQuantity != 0)
            {
                orderCount++;
            }
            entry = entry.Next;
        }

        return orderCount;
    }

    public uint GetLevelOrderQuantity()
    {
        uint orderQuantity = 0;
        if (IsEmpty)
            return orderQuantity;


        OrderbookEntry? entry = Head;
        while (entry != null)
        {
            orderQuantity += entry.CurrentQuantity;
            entry = entry.Next;
        }

        return orderQuantity;
    }


    public Side Side
    {
        get
        {
            if (IsEmpty)
                return Side.Unknown;
            else
                return Head!.IsBuySide ? Side.Buy : Side.Sell; // ! forgives the possibility of nullability 
        }
    }

    public List<OrderRecord> GetLevelOrderRecords()
    {
        List<OrderRecord> orderRecords = new List<OrderRecord>();
        OrderbookEntry? entry = Head;
        uint theoreticalQueuePosition = 0;
        while (entry != null)
        {
            if (entry.CurrentQuantity != 0)
                orderRecords.Add(new OrderRecord(
                    entry.OrderId,
                    entry.CurrentQuantity,
                    Price,
                    entry.IsBuySide,
                    entry.Username,
                    entry.SecurityId,
                    theoreticalQueuePosition
                ));

            theoreticalQueuePosition++;
            entry = entry.Next;
        }
        return orderRecords;
    }
}