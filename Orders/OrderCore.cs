namespace Valkyrie.Orders;

public class OrderCore(long orderId, long securityId, string username) : IOrderCore
{
    public long OrderId { get; } = orderId;
    public long SecurityId { get; } = securityId;
    public string Username { get; } = username;

}