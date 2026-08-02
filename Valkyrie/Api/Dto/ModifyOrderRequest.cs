using Valkyrie.Orders;

namespace Valkyrie.Api.Dto;

public record ModifyOrderRequest(
    long OrderId,
    long SecurityId,
    string Username,
    Side Side,
    long Price,
    uint Quantity
);