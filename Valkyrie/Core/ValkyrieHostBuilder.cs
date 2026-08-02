using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Valkyrie.Core.Configuration;
using Valkyrie.Logging;
using Valkyrie.Logging.Configuration;
using Valkyrie.MatchingEngine;
using Valkyrie.MatchingEngine.Algorithms;
using Valkyrie.MatchingEngine.Configuration;
using MatchingEngineSingleton = Valkyrie.MatchingEngine.MatchingEngine;

namespace Valkyrie.Core;

/// <summary>
/// stands up the application, it wires:
/// 1. configurations from the appsettings.json file
/// 2. DI... registers services so they can be auto-constructed
/// 3. Hosting.... starts and manages the life-cycle of long-running services
/// </summary>
public sealed class ValkyrieHostBuilder
{
    public static IHost BuildValkyrie()
        => Host.CreateDefaultBuilder()

            .UseContentRoot(AppContext.BaseDirectory)

            // DI stuff
            .ConfigureServices((
                    context, // --> loaded configurations aka appsettings.json
                    services // DI contianer stuff is loaded into
                    )
                =>
            {
                services.AddOptions(); // enables the IOptions<T> system

                // reads from the appsettings.json directly
                services.Configure<ValkyrieConfiguration>(
                    context.Configuration.GetSection(nameof(ValkyrieConfiguration)));
                services.Configure<LoggingConfiguration>(
                    context.Configuration.GetSection(nameof(LoggingConfiguration)));
                services.Configure<MatchingEngineConfiguration>(
                    context.Configuration.GetSection(nameof(MatchingEngineConfiguration)));

                // when any service asks for any of these, create one and give them that
                // and only ever create one instance (singleton) for the whole application lifetime
                services.AddSingleton<IValkyrie, Valkyrie>();
                services.AddSingleton<ITextLogger, TextLogger>();
                services.AddSingleton<IMatchingAlgorithm>(sp =>
                {
                    var configuration = sp.GetRequiredService<IOptions<MatchingEngineConfiguration>>().Value;
                    return configuration.Algorithm switch
                    {
                        MatchingAlgorithmType.Fifo => Fifo.Instance,
                        MatchingAlgorithmType.ProRata => ProRata.Instance,
                        _ => throw new InvalidOperationException(
                            $"Algorithm '{configuration.Algorithm}' not supported")
                    };
                });

                // add hosted service
                services.AddHostedService<Valkyrie>();
                services.AddSingleton<IMatchingEngine, MatchingEngineSingleton>();
            }).Build();
}