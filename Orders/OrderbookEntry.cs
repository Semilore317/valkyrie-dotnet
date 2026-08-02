namespace Valkyrie.Orders;

public class OrderbookEntry : Order
{
    public Limit ParentLimit { get; }
    public DateTime CreationTime { get; }
    public OrderbookEntry? Next { get; set; }
    public OrderbookEntry? Previous { get; set; }

    // i'm avoiding primary constructors here because it's less dense without it in this scenario
    public OrderbookEntry(Order order, Limit parentLimit) :
        base(order.OrderId, order.SecurityId, order.Username, order.Side, order.Price, order.CurrentQuantity)
    {
        CreationTime = DateTime.UtcNow;
        ParentLimit = parentLimit;
    }
}