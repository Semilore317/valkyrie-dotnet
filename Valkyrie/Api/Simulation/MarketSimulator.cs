using Microsoft.Extensions.Options;
using Valkyrie.Core.Configuration;
using Valkyrie.Logging;

namespace Valkyrie.Api.Simulation;

/// <summary>
/// implements a stochastic* market simulation using a random walk for price movements
/// and a poisson arrival process(Jitter) for "realistic" order delays
/// </summary>
public class MarketSimulator(
    IMarketDataSource source,
    ITextLogger logger,
    IOptions<MarketSimulatorConfiguration> config
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken token)
    {
        if (!config.Value.Enabled)
        {
            logger.Info("MarketSimulator", "disabled");
            return Task.CompletedTask;
        }
        logger.Info("MarketSimulatorRunner", $"Starting Data Source: {source.Name}");
        return source.RunAsync(token);
    }
}