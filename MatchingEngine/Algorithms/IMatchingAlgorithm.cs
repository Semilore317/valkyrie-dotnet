using Valkyrie.Orders;

namespace Valkyrie.MatchingEngine.Algorithms;

/// <summary>
/// Matches an incoming aggressor order against the resting book and returns the fills
/// </summary>
public interface IMatchingAlgorithm
{
    MatchResult MatchIncoming(
        Order incoming,
        SortedSet<Limit> bidLimits,
        SortedSet<Limit> askLimits,
        Dictionary<long, OrderbookEntry> orders);
}