using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Valkyrie.Api;
using Valkyrie.Api.Executions;
using Valkyrie.Api.MarketData;
using Valkyrie.Api.Simulation;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Api.Simulation.Lobster.Input;
using Valkyrie.Api.Simulation.Lobster.Services;
using Valkyrie.Core.Configuration;
using Valkyrie.Instrument.Configuration;
using Valkyrie.Instruments;
using Valkyrie.Logging;
using Valkyrie.Logging.Configuration;
using Valkyrie.MatchingEngine;
using Valkyrie.MatchingEngine.Algorithms;
using Valkyrie.MatchingEngine.Configuration;

static void InitializeOrderBooks(IHost app)
{
    var engine = app.Services.GetRequiredService<IMatchingEngine>();
    var config = app.Services.GetRequiredService<IConfiguration>();
    var instruments = config.GetSection("Instruments")
        .Get<List<InstrumentConfiguration>>() ?? [];

    foreach (var instrument in instruments)
    {
        engine.AddOrderBook(new Security(instrument.SecurityId, instrument.Symbol));
    }
}

var builder = WebApplication.CreateBuilder(args);

// configurations reading from appsettings.json
builder.Services.Configure<MarketSimulatorConfiguration>(
    builder.Configuration.GetSection(nameof(MarketSimulatorConfiguration)));
builder.Services.Configure<LoggingConfiguration>(
    builder.Configuration.GetSection(nameof(LoggingConfiguration)));
builder.Services.Configure<ValkyrieConfiguration>(
    builder.Configuration.GetSection(nameof(ValkyrieConfiguration)));
builder.Services.Configure<MatchingEngineConfiguration>(
    builder.Configuration.GetSection(nameof(MatchingEngineConfiguration)));

// core domain & services
builder.Services.AddSingleton<ITextLogger, TextLogger>();
builder.Services.AddSingleton<IMatchingAlgorithm>(sp =>
{
    var config = sp.GetRequiredService<IOptions<MatchingEngineConfiguration>>().Value;
    return config.Algorithm switch
    {
        MatchingAlgorithmType.Fifo => Fifo.Instance,
        MatchingAlgorithmType.ProRata => ProRata.Instance,
        _ => throw new InvalidOperationException($"Algorithm '{config.Algorithm}' is not supported.")
    };
});

builder.Services.AddSingleton<IMatchingEngine, MatchingEngine>();
builder.Services.AddSingleton<OrderGateway>();
builder.Services.AddSingleton<IExecutionJournal, InMemoryExecutionJournal>();

// market data & transport
builder.Services.AddSingleton<MarketDataHub>();
builder.Services.AddSingleton<IMarketDataPublisher, WebSocketMarketDataPublisher>();
builder.Services.ConfigureHttpJsonOptions(
    o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
/* Historical playback instead of seeding synthetic data */
builder.Services.AddSingleton<ILobsterInputProvider, CsvLobsterInputProvider>();
builder.Services.AddSingleton<ILobsterInputProvider, ZipLobsterInputProvider>();
builder.Services.AddSingleton<IReplayDelay, SystemReplayDelay>();

// selectable market-data sources
builder.Services.AddSingleton<SyntheticMarketSource>();
builder.Services.AddSingleton<LobsterReplayMarketSource>();

builder.Services.AddSingleton<LobsterReplayReader>();

builder.Services.AddSingleton<IMarketDataSource>(
    services => 
    {
        var configuration = services
        .GetRequiredService<IOptions<MarketSimulatorConfiguration>>()
        .Value;

        return configuration.Source switch
        {
            MarketDataSourceType.Synthetic => services.GetRequiredService<SyntheticMarketSource>(),
            MarketDataSourceType.LobsterReplay => services.GetRequiredService<LobsterReplayMarketSource>(),
            _ => throw new InvalidOperationException($"Market-data source {configuration.Source} not supported")
        };
    });

// hosted services
builder.Services.AddHostedService<Valkyrie.Core.Valkyrie>(); // the background service... it still runs
builder.Services.AddHostedService<Valkyrie.Api.Simulation.MarketSimulator>();

// initialization
var app = builder.Build();
InitializeOrderBooks(app);

// endpoints
app.UseWebSockets(); // turns on the 101 middleware
app.MapOrderEndpoints();
app.MapExecutionEndpoints();
app.MapMarketDataEndpoints(); // registers /ws/marketdata

await app.RunAsync();

public partial class Program; // exposed for WebApplicationFactory 