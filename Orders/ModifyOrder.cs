namespace Valkyrie.Orders;

public class ModifyOrder(
        long orderId,
        long securityId,
        string username,
        Side side,
        long price,
        uint quantity
    ) : IOrderCore
{
    public long OrderId { get; } = orderId;
    public long SecurityId { get; } = securityId;
    public string Username { get; } = username;

    public long Price { get; } = price;
    public uint Quantity { get; } = quantity;
    public Side Side { get; } = side;


    public bool IsBuySide => Side == Side.Buy;

    public Order ToNewOrder() => new Order(
        OrderId,
        SecurityId,
        Username,
        Side,
        Price,
        Quantity
    );


    public CancelOrder ToCancelOrder() => new CancelOrder(
        OrderId,
        SecurityId,
        Username
    );
}