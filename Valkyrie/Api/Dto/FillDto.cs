namespace Valkyrie.Api.Dto;

public record FillDto(
    long BidOrderId,
    long AskOrderId,
    long Price,
    uint Quantity
    );