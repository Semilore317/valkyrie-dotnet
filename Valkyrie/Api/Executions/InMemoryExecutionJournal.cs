using Valkyrie.MatchingEngine;
using Valkyrie.Orders;

namespace Valkyrie.Api.Executions;

public class InMemoryExecutionJournal : IExecutionJournal
{
    // order id => session ownership information
    private readonly object _gate = new();

    // append-only log for the lifetime of the trading "session"
    private readonly Dictionary<long, TrackedOrder> _orders = [];
    private readonly List<ExecutionRecord> _executions = [];

    public void RegisterOrder(Guid sessionId, long orderId, long securityId, Side side, uint quantity)
    {
        lock (_gate)
        {
            _orders[orderId] = new TrackedOrder
            {
                SessionId = sessionId,
                SecurityId = securityId,
                OrderId = orderId,
                Side = side,
                OriginalQuantity = quantity,
                RemainingQuantity = quantity
            };
        }
    }

    public void ModifyOrder(long orderId, Side side, uint quantity)
    {
        lock (_gate)
        {
            if (!_orders.TryGetValue(orderId, out var tracked))
                return;

            tracked.Side = side;
            tracked.OriginalQuantity = quantity;
            tracked.RemainingQuantity = quantity;
        }
    }

    public void RecordFills(IReadOnlyList<Fill> fills, long incomingOrderId)
    {
        lock (_gate)
        {
            foreach (var fill in fills)
            {
                // both trader-facing execution records share one match ID
                var matchId = Guid.NewGuid();

                RecordOrderSide(
                    fill,
                    fill.BidOrderId,
                    Side.Buy,
                    incomingOrderId,
                    matchId
                );

                RecordOrderSide(
                    fill,
                    fill.AskOrderId,
                    Side.Sell,
                    incomingOrderId,
                    matchId
                );
            }
        }
    }

    public void RemoveOrder(long orderId)
    {
        lock(_gate)
            _orders.Remove(orderId);    
    }

    public IReadOnlyList<ExecutionRecord> GetExecutions(Guid sessionId, long? securityId = null)
    {
        lock (_gate)
        {
            return _executions
                .Where(execution => execution.SessionId == sessionId)
                .Where(execution => securityId == null || execution.SecurityId == securityId.Value)
                .OrderByDescending(execution => execution.ExecutedAt)
                .ToList();
        }
    }

    private void RecordOrderSide(
        Fill fill,
        long orderId,
        Side side,
        long incomingOrderId,
        Guid matchId
    )
    {
        // simulator and untracked orders are intentionally ignored
        if (_orders.TryGetValue(orderId, out var tracked))
            return;

        var role = orderId == incomingOrderId
            ? LiquidityRole.Taker
            : LiquidityRole.Maker;

        if (tracked != null)
            _executions.Add(new ExecutionRecord(
                ExecutionId: Guid.NewGuid(),
                MatchId: matchId,
                SessionId: tracked.SessionId,
                SecurityId: tracked.SecurityId,
                OrderId: orderId,
                Side: side,
                Price: fill.ExecutionPrice,
                Quantity: fill.FilledQuantity,
                ExecutedAt: fill.FilledAt,
                LiquidityRole: role
            ));

        tracked!.RemainingQuantity = fill.FilledQuantity >= tracked.RemainingQuantity
            ? 0
            : tracked.RemainingQuantity - fill.FilledQuantity;

        if (tracked.RemainingQuantity == 0)
            _orders.Remove(orderId);
    }
}