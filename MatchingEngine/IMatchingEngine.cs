using System.Diagnostics.CodeAnalysis;
using Valkyrie.Instruments;
using Valkyrie.Orders;

namespace Valkyrie.MatchingEngine;

public interface IMatchingEngine
{
    void AddOrderBook(Security instrument);
    MatchResult AddOrder(Order order);
    MatchResult ChangeOrders(ModifyOrder modifyOrder);
    void RemoveOrder(CancelOrder cancelOrder);
    bool TryGetSnapshot(long securityId, [MaybeNullWhen(false)] out OrderBookSnapshot snapshot);
}