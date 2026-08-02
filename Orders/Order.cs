namespace Valkyrie.Orders;

public class Order(long orderId, long securityId, string username, Side side, long price, uint initialQuantity)
    : OrderCore(orderId, securityId, username)
{
    public Side Side { get; } = side;
    public long Price { get; } = price; // in cents
    public uint InitialQuantity { get; } = initialQuantity;
    public bool IsBuySide => Side == Side.Buy;


    // currentQUantity initially matches initialQuantity
    public uint CurrentQuantity { get; private set; } = initialQuantity;


    public void DecrementQuantity(uint amount)
    {
        if (amount > CurrentQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Decrement amount cannot be greater than the current quantity.");
        }
        else
        {
            CurrentQuantity -= amount;
        }
    }

    public void IncrementQuantity(uint amount)
    {
        CurrentQuantity += amount;
    }
}