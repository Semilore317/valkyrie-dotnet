namespace Valkyrie.MatchingEngine;

public record Level(long Price, long Quantity);

public record OrderBookSnapshot(
    long SecurityId,
    long? Bid,
    long? Ask,
    long? Spread,
    IReadOnlyList<Level> Bids,
    IReadOnlyList<Level> Asks);