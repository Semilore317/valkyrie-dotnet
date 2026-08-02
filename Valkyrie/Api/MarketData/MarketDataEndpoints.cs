using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Core.Configuration;

namespace Valkyrie.Api.MarketData;

public static class MarketDataEndpoints
{
    private record ClientMessage(string Action, long SecurityId);
    private record MarketDataStatusResponse(
        string Mode,
        string Liquidity,
        bool OrderEntryEnabled,
        double? PlaybackSpeed
    );

    public static void MapMarketDataEndpoints(this WebApplication app)
    {
        app.MapGet("/market-data/status", (
            IOptions<MarketSimulatorConfiguration> options) =>
        {
            var configuration = options.Value;

            var mode = configuration.Enabled
                ? configuration.Source switch
                {
                    MarketDataSourceType.Synthetic => "synthetic",
                    MarketDataSourceType.LobsterReplay => "historicalReplay",
                    _ => throw new InvalidOperationException(
                        $"Market-data source {configuration.Source} not supported")
                }
                : "manual";

            var isHistoricalReplay = configuration.Enabled &&
                                     configuration.Source == MarketDataSourceType.LobsterReplay;
            double? playbackSpeed = isHistoricalReplay
                ? configuration.HistoricalReplay.PlaybackSpeed
                : null;

            return Results.Ok(new MarketDataStatusResponse(
                mode,
                isHistoricalReplay ? "observational" : "executable",
                !isHistoricalReplay,
                playbackSpeed
            ));
        });

        app.Map("/ws/marketdata", async (HttpContext context, MarketDataHub hub, OrderGateway gateway)
            =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            // this waits for the 101 status code so the app starts using ws to communicate with the browser
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var connection = new MarketDataConnection(socket);

            // kick off the background loop that empties this connection's outbound queue into the socket
            var sending = connection.SendLoopAsync(context.RequestAborted);

            // honestly overkill... but it maps neatly to a single frame...
            // no point causing internal fragmentation for no gain 
            var buffer = new byte[4096];

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, context.RequestAborted);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure, "closing", context.RequestAborted);
                        break;
                    }

                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var message = JsonSerializer.Deserialize<ClientMessage>(text,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));

                    if (message == null)
                        continue;

                    if (message.Action == "subscribe")
                    {
                        // send a snapshot ASAP so the client isn't blind till the next trade
                        hub.Subscribe(connection, message.SecurityId);

                        if (gateway.TryGetBook(message.SecurityId, out var snapshot))
                            if (snapshot != null)
                                connection.Enqueue(JsonSerializer.SerializeToUtf8Bytes(
                                    new
                                    {
                                        type = "book",
                                        snapshot.SecurityId,
                                        snapshot.Bid,
                                        snapshot.Ask,
                                        snapshot.Spread,
                                        snapshot.Bids,
                                        snapshot.Asks
                                    }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                    }
                    else if (message.Action == "unsubscribe")
                    {
                        hub.Unsubscribe(connection, message.SecurityId);
                    }
                }
            }
            catch (OperationCanceledException) // at some point the client is gonna disappear 
            {
                /*No_Op*/
            }
            finally
            {
                hub.RemoveEveryWhere(connection);
                connection.Complete(); // allow the send loop finish
                try
                {
                    await sending;
                }
                catch (OperationCanceledException)
                {
                    /*NO_OP*/
                }
            }
        });
    }
}
