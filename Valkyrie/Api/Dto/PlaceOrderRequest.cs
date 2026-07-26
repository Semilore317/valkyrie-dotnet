using Valkyrie.Orders;

namespace Valkyrie.Api.Dto;

public record PlaceOrderRequest(
    long SecurityId,
    string Username,
    Side Side,
    long Price,
    uint Quantity,
    Guid? SessionId = null
);