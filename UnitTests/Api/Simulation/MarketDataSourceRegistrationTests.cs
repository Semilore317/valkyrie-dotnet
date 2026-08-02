using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Valkyrie.Api.Simulation;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Api.Simulation.Lobster.Services;

namespace UnitTests.Api.Simulation;

public sealed class MarketDataSourceRegistrationTests
{
    [Theory]
    [InlineData(MarketDataSourceType.Synthetic, typeof(SyntheticMarketSource))]
    [InlineData(MarketDataSourceType.LobsterReplay, typeof(LobsterReplayMarketSource))]
    public void ConfiguredSource_SelectsExpectedImplementation(
        MarketDataSourceType configuredSource,
        Type expectedType
        )
    {
        using var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MarketSimulatorConfiguration:Enabled"] = "false",
                    ["MarketSimulatorConfiguration:Source"] = configuredSource.ToString()
                });
            }));

        var selectedSource = app.Services
            .GetRequiredService<IMarketDataSource>();

        selectedSource.GetType()
            .Should().Be(expectedType);
    }

}
