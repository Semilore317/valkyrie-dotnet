namespace Valkyrie.Orders;
/// <summary>
/// /// Represents a single order in the matching engine's internal book state.
/// Market data granularity, for reference:
///   L1 - Top of book: best bid/ask price and size only.
///   L2 - Market depth: bid/ask prices with aggregated size at each level.
///   L3 - Market by order: every individual order, unaggregated, with its own
///        identity and price-time priority. This record operates at L3 granularity -
///        it's what the matching engine needs to track exact queue position within
///        a price level, even though the engine may only publish L1/L2 externally.
///   L4 - not standardized... just implies more data
/// </summary>
public record OrderRecord(
  long OrderId,
  uint Quantity,
  long Price,
  bool IsBuySide,
  string Username,
  long SecurityId,
  uint TheoreticalQueuePosition
);