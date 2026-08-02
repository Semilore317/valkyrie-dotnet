using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UnitTests.Api;

public sealed class InstrumentApiTests
{
    private sealed record class InstrumentResponse(
        long SecurityId,
        string Ticker,
        string Name
    );

    [Fact]
    public async Task GetInstruments_ReturnsConfiguredCatalog()
    {
        using var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var instruments = await client
            .GetFromJsonAsync<List<InstrumentResponse>>("/instruments");

        instruments.Should().BeEquivalentTo(
            new[]
            {
                new InstrumentResponse(
                    1L,
                    "MSFT",
                    "Microsoft Corporation"),
                new InstrumentResponse(
                    2L,
                    "AAPL",
                    "Apple Inc."),
                new InstrumentResponse(
                    3L,
                    "AMZN",
                    "Amazon.com, Inc."),
                new InstrumentResponse(
                    4L,
                    "GOOG",
                    "Alphabet Inc."),
                new InstrumentResponse(
                    5L,
                    "INTC",
                    "Intel Corporation"),
            }, options => options.WithStrictOrdering());
    }
}
