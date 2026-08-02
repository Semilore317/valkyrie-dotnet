namespace Valkyrie.Orders;

public interface IOrderCore
{
    // since these are read-only we can omit the setters
    long OrderId { get; }
    long SecurityId { get; }
    string Username { get; }
}