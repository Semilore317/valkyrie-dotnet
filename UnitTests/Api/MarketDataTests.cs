using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Valkyrie.Api.Dto;
using Valkyrie.Api.MarketData;
using Valkyrie.MatchingEngine;
using Valkyrie.Orders;

namespace UnitTests.Api;

public class MarketDataTests
{
    private sealed class CapturingPublisher : IMarketDataPublisher
    {
        public List<TradeEvent> Trades { get; } = new();
        public List<OrderBookSnapshot> BookSnapshots { get; } = new();
        public List<MarketTradeEvent>  MarketTrades  {get;} = [];
        public void PublishTrade(TradeEvent trade) => Trades.Add(trade);
        public void PublishBook(OrderBookSnapshot bookSnapshot) => BookSnapshots.Add(bookSnapshot);
        public void PublishMarketTrade(MarketTradeEvent marketTradeEvent) => MarketTrades.Add(marketTradeEvent);
    }

    private static (WebApplicationFactory<Program>, CapturingPublisher ) NewApp()
        {
            var publisher = new CapturingPublisher();
            var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.ConfigureServices(service =>
                {
                    service.RemoveAll<IMarketDataPublisher>();
                    service.AddSingleton<IMarketDataPublisher>(publisher);
                }));

            return (app, publisher);
        }

    [Fact]
    public async Task Resting_Order_Publishes_Book_But_No_Trade()
    {
        var (app, publisher) = NewApp();

        // Resource cleanup
        // using disposes these at the end of this method's execution
        // they're cleaned up in reverse order
        using var _ = app;
        using var client = app.CreateClient();

        await client.PostAsJsonAsync(
            "/orders", 
            new PlaceOrderRequest(1, "sam", Side.Sell, 10000, 100));
        
        publisher.Trades.Should().BeEmpty();
        publisher.BookSnapshots.Should().ContainSingle().Which.SecurityId.Should().Be(1);
    }

    [Fact]
    public async Task Crossing_Order_Publishes_Trade_And_Book()
    {
        var (app, publisher) = NewApp();

        using var _app = app;
        using var client = app.CreateClient();
        
        await client.PostAsJsonAsync(
            "/orders", 
            new PlaceOrderRequest(1, "sam", Side.Sell, 10000, 100));
        await client.PostAsJsonAsync(
            "/orders",
            new PlaceOrderRequest(1, "sam", Side.Buy, 10000, 70));


        publisher.Trades.Should().ContainSingle();
        publisher.Trades.Should().ContainSingle().Which.SecurityId.Should().Be(1);
        publisher.Trades[0].Quantity.Should().Be(70u);
        publisher.BookSnapshots.Last().SecurityId.Should().Be(1); // book republished after the cross
        publisher.Trades.Single().AggressorSide.Should().Be(Side.Buy);
        publisher.MarketTrades.Should().BeEmpty();
    }
}