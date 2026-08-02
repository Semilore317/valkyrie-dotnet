namespace Valkyrie.Orders;

/// <summary>
/// due to the nature of the way we're presenting the bids and asks
/// asks: ascending order --> buyers want the least limit
/// bids: descending order --> sellers want the highest limit
/// we need to be able to clearly compare and sort them
/// </summary>
public class BidLimitComparer : IComparer<Limit>
{
    public static IComparer<Limit> Comparer { get; } = new BidLimitComparer();

    public int Compare(Limit? x, Limit? y)
    {
        // early returns
        if (x == null && y == null)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        return y.Price.CompareTo(x.Price);
    }
}

public class AskLimitComparer : IComparer<Limit>
{
    public static IComparer<Limit> Comparer { get; } = new AskLimitComparer();

    public int Compare(Limit? x, Limit? y)
    {
        // early returns
        if (x == null && y == null)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        return x.Price.CompareTo(y.Price);
    }
}