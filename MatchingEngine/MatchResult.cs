using Valkyrie.Orders;

namespace Valkyrie.MatchingEngine;

public class MatchResult(
    List<Fill> fills
)
{
    public List<Fill> Fills { get; } = fills;
    public bool IsMatch => Fills.Any(); // returns true if fills is NOT empty
}