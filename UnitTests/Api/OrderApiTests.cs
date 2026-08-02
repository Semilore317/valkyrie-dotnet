using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Valkyrie.Api;
using Valkyrie.Api.Dto;
using Valkyrie.MatchingEngine;
using Valkyrie.Orders;

namespace UnitTests.Api;

public class OrderApiTests
{
    private static WebApplicationFactory<Program> NewApp() => new();

    // since these are async it's best to use tasks
    [Fact]
    public async Task Post_RestsOrder_AndBookReflectsIt()
    {
        using var app = NewApp();
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/orders",
            new PlaceOrderRequest(SecurityId: 1, "sam", Side.Sell, Price: 10000, Quantity: 100));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var ack = await response.Content.ReadFromJsonAsync<OrderAck>();

        ack!.Matched.Should().BeFalse();

        var book = await client.GetFromJsonAsync<OrderBookSnapshot>($"/book/1");
        book!.Ask.Should().Be(10000);
        book.Bids.Should().BeEmpty();

        book.Asks.Should().ContainSingle().Which.Quantity.Should().Be(100);
    }

    [Fact]
    public async Task Post_CrossingOrder_Fills()
    {
        using var app = NewApp();
        using var client = app.CreateClient();

        await client.PostAsJsonAsync("/orders",
            new PlaceOrderRequest(1, "sam", Side.Sell, 10000, 100));

        var response = await client.PostAsJsonAsync("/orders",
            new PlaceOrderRequest(1, "lee", Side.Buy, 10000, 60));

        var ack = await response.Content.ReadFromJsonAsync<OrderAck>();

        ack!.Matched.Should().BeTrue();

        ack.Fills.Should().ContainSingle().Which.Quantity.Should().Be(60u);

        var book = await client.GetFromJsonAsync<OrderBookSnapshot>($"/book/1");

        book!.Asks.Should().ContainSingle().Which.Quantity.Should().Be(40);
    }

    [Fact]
    public async Task Delete_RemovesRestingOrder()
    {
        using var app = NewApp();
        using var client = app.CreateClient();

        var ack = await (await client.PostAsJsonAsync("/orders",
            new PlaceOrderRequest(1, "sam", Side.Sell, 10000, 100)))
            .Content.ReadFromJsonAsync<OrderAck>();

        var delete = await client.DeleteAsync($"/instruments/1/orders/{ack!.OrderId}?username=sam");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);


        var book = await client.GetFromJsonAsync<OrderBookSnapshot>("/book/1");
        book!.Asks.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_UnknownSecurity_Returns404()
    {
        using var app = NewApp();
        using var client = app.CreateClient();

        var response = await client.PostAsync("/orders",
            new StringContent("Not Valid JSON", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}