using System.Text.Json.Serialization;
using Instruments;
using Microsoft.Extensions.Options;
using Valkyrie.Api;
using Valkyrie.Api.Executions;
using Valkyrie.Api.MarketData;
using Valkyrie.Api.Simulation;
using Valkyrie.Core.Configuration;
using Valkyrie.Instrument.Configuration;
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
builder.Services.AddSingleton<IMarketDataSource, SyntheticMarketSource>();

// hosted services
builder.Services.AddHostedService<Valkyrie.Core.Valkyrie>(); // the background service... it still runs
builder.Services.AddHostedService<Valkyrie.Api.Simulation.MarketSimulator>();

// initialization
var app = builder.Build();
InitializeOrderBooks(app);

// pipeling & endpoints
app.UseWebSockets(); // turns on the 101 middleware
app.MapOrderEndpoints();
app.MapMarketDataEndpoints(); // registers /ws/marketdata

await app.RunAsync();

public partial class Program; // exposed for WebApplicationFactory 