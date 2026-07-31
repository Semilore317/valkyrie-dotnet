using Valkyrie.Api.Dto;
using Valkyrie.Api.Executions;
using Valkyrie.Api.MarketData;
using Valkyrie.MatchingEngine;
using Valkyrie.Orders;

namespace Valkyrie.Api;

/// <summary>
///  provides a thread-safe entry point for order operations routing to the matching engine
///  It uses a private lock object pattern to guarantee a mutex across all state mutations and reads
/// </summary>
public sealed class OrderGateway(
    IMatchingEngine engine,
    IMarketDataPublisher publisher,
    IExecutionJournal executionJournal
)
{
    private readonly object _gate = new();
    private long _nextOrderId;

    private void Broadcast(
        MatchResult result,
        OrderBookSnapshot? snapshot,
        Side aggressorSide
    )
    {
        foreach (var fill in result.Fills)
            publisher.PublishTrade(TradeEvent.From(fill, aggressorSide));

        if (snapshot != null)
            publisher.PublishBook(snapshot);
    }

    public OrderAck Submit(PlaceOrderRequest request)
    {
        MatchResult result;
        long id;
        OrderBookSnapshot? snapshot;

        // syncs access to the sequential ID generation and the underlying non-thread-safe matching engine instance
        // this prevents lock contention from external code
        lock (_gate)
        {
            // server assigns the id for now.... since it's locked it's not a major concern for now
            id = ++_nextOrderId;


            // only user/browser orders carry a session
            // simulator orders remain outside the trader execution journal
            if (request.SessionId is { } sessionId)
            {
                executionJournal.RegisterOrder(
                    sessionId: sessionId,
                    orderId: id,
                    securityId: request.SecurityId,
                    side: request.Side,
                    quantity: request.Quantity
                );
            }

            result = engine.AddOrder(
                new Order(
                    orderId: id,
                    securityId: request.SecurityId,
                    username: request.Username,
                    side: request.Side,
                    price: request.Price,
                    initialQuantity: request.Quantity
                )
            );

            // Keep journal state and the published snapshot consistent with
            // the matching-engine mutation performed under this gateway lock.
            executionJournal.RecordFills(result.Fills, id);

            engine.TryGetSnapshot(request.SecurityId, out snapshot);
        }

        Broadcast(result, snapshot, aggressorSide: request.Side);
        return OrderAck.From(id, result);
    }

    public void Cancel(long id, long securityId, string username)
    {
        OrderBookSnapshot? snapshot;
        lock (_gate)
        {
            engine.RemoveOrder(new CancelOrder(id, securityId, username));

            executionJournal.RemoveOrder(id);

            engine.TryGetSnapshot(securityId, out snapshot);
        }

        if (snapshot != null)
            publisher.PublishBook(snapshot);
    }

    public bool TryGetBook(long securityId, out OrderBookSnapshot? book)
    {
        lock (_gate)
        {
            return engine.TryGetSnapshot(securityId, out book);
        }
    }

    public OrderAck Modify(long id, long securityId, ModifyOrderRequest request)
    {
        MatchResult result;
        OrderBookSnapshot? snapshot;

        lock (_gate)
        {
            executionJournal.ModifyOrder(
                orderId: id,
                side: request.Side,
                quantity: request.Quantity
            );

            var modifyOrder = new ModifyOrder(
                id,
                securityId,
                request.Username,
                request.Side,
                request.Price,
                request.Quantity
            );
            result = engine.ChangeOrders(modifyOrder);
            executionJournal.RecordFills(result.Fills, id);
            engine.TryGetSnapshot(securityId, out snapshot);
        }

        Broadcast(result, snapshot, aggressorSide: request.Side);
        return OrderAck.From(id, result);
    }
}