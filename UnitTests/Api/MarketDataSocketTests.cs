using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Valkyrie.Api.Dto;
using Valkyrie.Orders;

namespace UnitTests.Api;

public class MarketDataSocketTests
{
    // reads one full text message (across frames hence the size) and parses it as JSON
    private static async Task<JsonDocument> ReceiveJson(WebSocket socket, CancellationToken token)
    {
        var buffer = new byte[4096];
        using var stream = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, token);
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        stream.Position = 0;
        return await JsonDocument.ParseAsync(stream, cancellationToken: token);
    }


    [Fact]
    public async Task Subscriber_Receives_Trade_Frame_On_Cross()
    {
        // real publisher
        using var app = new WebApplicationFactory<Program>();
        using var http = app.CreateClient();

        // 5s limit so a missing frame FAILs the test rather than hanging
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var wsClient = app.Server.CreateWebSocketClient();

        var uri = new UriBuilder(app.Server.BaseAddress)
        {
            Scheme = "ws",
            Path = "/ws/marketdata"
        }.Uri;

        using var socket = await wsClient.ConnectAsync(uri, cts.Token);

        // subcribe
        await socket.SendAsync(
            Encoding.UTF8.GetBytes("""{"action":"subscribe", "securityId":"1"}"""),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken: cts.Token);


        // read the initial book snapshot.
        // this is essentially the sync barrier
        // once it arrives, we can ASSERT that subscribe ran, so the following POSTS CANNOT be missed
        using (var first = await ReceiveJson(socket, cts.Token))
            first.RootElement.GetProperty("type")
                .GetString().Should().Be("book");

        await http.PostAsJsonAsync(
            "/orders",
            new PlaceOrderRequest(
                1,
                "sam",
                Side.Sell,
                10000,
                100), cts.Token);

        await http.PostAsJsonAsync(
            "/orders",
            new PlaceOrderRequest(
                1,
                "cat",
                Side.Buy,
                10000,
                70
            ), cts.Token);

        // incoming frames: book (from the resting null), then trade + book (from the cross)
        string? type = null;
        while (type != "trade")
            using (var doc = await ReceiveJson(socket, cts.Token))
                type = doc.RootElement.GetProperty("type").GetString();

        type.Should().Be("trade");
    }
}