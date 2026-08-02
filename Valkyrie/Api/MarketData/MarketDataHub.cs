using System.Collections.Concurrent;

namespace Valkyrie.Api.MarketData;

public class MarketDataHub
{
    // securityId ==> (connectionID ==> connection)
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, MarketDataConnection>>
        _bySecurity = new();

    public void Subscribe(MarketDataConnection connection, long securityId)
    {
        _bySecurity.GetOrAdd(securityId, _ => new ConcurrentDictionary<string, MarketDataConnection>())
            .TryAdd(connection.Id, connection);
    }

    public void Unsubscribe(MarketDataConnection connection, long securityId)
    {
        if (_bySecurity.TryGetValue(securityId, out var set))
            set.TryRemove(connection.Id, out _);
    }

    public void RemoveEveryWhere(MarketDataConnection connection) // on disconnect
    {
        foreach (var set in _bySecurity.Values)
            set.TryRemove(connection.Id, out _);
    }

    public void BroadCast(long securityId, byte[] message)
    {
        if (!_bySecurity.TryGetValue(securityId, out var set)) return;
        foreach (var connection in set.Values)
            connection.Enqueue(message); // enqueue rather then sending
    }


}