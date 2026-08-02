using Valkyrie.Api.Dto;
using Valkyrie.MatchingEngine;

namespace Valkyrie.Api;

public record OrderAck(long OrderId, bool Matched, IReadOnlyList<FillDto> Fills)
{
    public static OrderAck From(long id, MatchResult matchResult)
    {
        return new(id,
            matchResult.IsMatch,
            matchResult.Fills.Select(
                f => new FillDto(
                    f.BidOrderId, f.AskOrderId, f.ExecutionPrice, f.FilledQuantity)).ToList()
            );
    }
}