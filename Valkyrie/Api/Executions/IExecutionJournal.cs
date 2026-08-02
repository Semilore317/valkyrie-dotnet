using Valkyrie.MatchingEngine;
using Valkyrie.Orders;

namespace Valkyrie.Api.Executions;

public interface IExecutionJournal
{
    /// <summary>
    /// associates an exchange order id with a traading session
    /// </summary>
    void RegisterOrder
    (
        Guid sessionId,
        long orderId,
        long securityId,
        Side side,
        uint quantity
    );

    /// <summary>
    /// updates metadata when a resting order is modified
    /// </summary>
    void ModifyOrder(
        long orderId,
        Side side,
        uint quantity
    );

    /// <summary>
    /// convert matching engine fills to session execution records
    /// </summary>
    void RecordFills(
        IReadOnlyList<Fill> fills,
        long incomingOrderId
    );

    // stop tracking an order after cancellation
    void RemoveOrder(long orderId);

    IReadOnlyList<ExecutionRecord> GetExecutions(
        Guid sessionId,
        long? securityId = null
    );
}