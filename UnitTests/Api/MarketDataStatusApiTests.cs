using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Valkyrie.Api.Simulation.Lobster.Enums;

namespace UnitTests.Api;

public sealed class MarketDataStatusApiTests
{
    private sealed record MarketDataStatusResponse(
        string Mode,
        string Liquidity,
        bool OrderEntryEnabled,
        double? PlaybackSpeed
    );

    [Theory]
    [InlineData(false, MarketDataSourceType.Synthetic, "manual", "executable", true, null)]
    [InlineData(true, MarketDataSourceType.Synthetic, "synthetic", "executable", true, null)]
    [InlineData(true, MarketDataSourceType.LobsterReplay, "historicalReplay", "observational", false, 2.5)]
    public async Task GetMarketDataStatus_ReturnsEffectiveMode(
        bool simulatorEnabled,
        MarketDataSourceType source,
        string expectedMode,
        string expectedLiquidity,
        bool expectedOrderEntryEnabled,
        double? expectedPlaybackSpeed
    )
    {
        using var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["MarketSimulatorConfiguration:Enabled"] = simulatorEnabled.ToString(),
                            ["MarketSimulatorConfiguration:Source"] = source.ToString(),
                            ["MarketSimulatorConfiguration:HistoricalReplay:PlaybackSpeed"] = "2.5"
                        });
                });

                builder.ConfigureServices(services =>
                    services.RemoveAll<IHostedService>());
            });

        using var client = app.CreateClient();

        var status = await client.GetFromJsonAsync<MarketDataStatusResponse>(
            "/market-data/status");

        status.Should().BeEquivalentTo(new MarketDataStatusResponse(
            expectedMode,
            expectedLiquidity,
            expectedOrderEntryEnabled,
            expectedPlaybackSpeed
        ));
    }
}
