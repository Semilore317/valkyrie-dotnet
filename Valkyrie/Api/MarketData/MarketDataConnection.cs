using System.Net.WebSockets;
using System.Threading.Channels;

namespace Valkyrie.Api.MarketData;

/// <summary>
/// a websocket cannot have two sendAsync calls in flight at once... it corrupts the frame
/// but two trades can fire from two threads simultaneously
/// this approach allows each connection to hold it's own an in-memory queue
/// publishers only enqueue (non-blocking and thread-safe) while writers use SendAsync
/// </summary>
public sealed class MarketDataConnection(WebSocket socket)
{
    private readonly Channel<byte[]> _outbound = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // if a client is slow... it's snapshots are stale, safe to drop them
        });

    public string Id { get; } = Guid.NewGuid().ToString("n");

    public void Enqueue(byte[] message)
    {
        _outbound.Writer.TryWrite(message);
    }

    public async Task SendLoopAsync(CancellationToken token)
    {
        await foreach (var message in _outbound.Reader.ReadAllAsync(token))
            await socket.SendAsync(message, WebSocketMessageType.Text, endOfMessage: true, token);
    }

    public void Complete()
    {
        _outbound.Writer.TryComplete();
    }

}