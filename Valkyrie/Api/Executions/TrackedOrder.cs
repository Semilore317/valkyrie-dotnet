using Valkyrie.Orders;

namespace Valkyrie.Api.Executions;

/// <summary>
/// minimal metadata needed to associate an order with the browser session that submitted it
/// </summary>
internal sealed class TrackedOrder
{
    public required Guid SessionId { get; init; }
    public required long SecurityId { get; init; }
    public required long OrderId { get; init; }
    public required Side Side { get; set; }
    public required uint OriginalQuantity { get; set; }
    public required uint RemainingQuantity { get; set; }
}