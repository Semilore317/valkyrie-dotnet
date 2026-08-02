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

static InstrumentCatalogue BuildInstrumentCatalogue(
    IConfiguration configuration)
{
    var configuredInstruments = configuration
        .GetSection("Instruments")
        .Get<List<InstrumentConfiguration>>() ?? [];

    var instruments = configuredInstruments.Select(instrument => new Security(
        instrument.SecurityId,
        instrument.Ticker,
        instrument.Name));

    return new InstrumentCatalogue(instruments);
}

static void InitializeOrderBooks(IHost app)
{
    var engine = app.Services
        .GetRequiredService<IMatchingEngine>();

    var instrumentCatalogue = app.Services
        .GetRequiredService<InstrumentCatalogue>();

    foreach (var instrument in instrumentCatalogue.Instruments)
        engine.AddOrderBook(instrument);
}

static ITextLogger CreateTextLogger(
    IServiceProvider serviceProvider)
{
    var options = serviceProvider
        .GetRequiredService<IOptions<LoggingConfiguration>>();

    return options.Value.LoggerType switch
    {
        LoggerType.Text => new TextLogger(options),
        LoggerType.Console => new ConsoleTextLogger(),
        _ => throw new InvalidOperationException(
            $"Logger type '{options.Value.LoggerType}' is not supported.")
    };
}

static string[] ReadAllowedOrigins(
    IConfiguration configuration)
{
    var corsConfiguration = configuration
        .GetSection(CorsConfiguration.SectionName)
        .Get<CorsConfiguration>() ?? new CorsConfiguration();

    return corsConfiguration.AllowedOrigins
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

const string frontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

var instrumentCatalogue = BuildInstrumentCatalogue(
    builder.Configuration);

var allowedOrigins = ReadAllowedOrigins(
    builder.Configuration);

if (builder.Environment.IsProduction() &&
    allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "At least one Cors:AllowedOrigins entry is required in Production.");
}

// Configuration
builder.Services.Configure<MarketSimulatorConfiguration>(
    builder.Configuration.GetSection(
        nameof(MarketSimulatorConfiguration)));

builder.Services.Configure<LoggingConfiguration>(
    builder.Configuration.GetSection(
        nameof(LoggingConfiguration)));

builder.Services.Configure<ValkyrieConfiguration>(
    builder.Configuration.GetSection(
        nameof(ValkyrieConfiguration)));

builder.Services.Configure<MatchingEngineConfiguration>(
    builder.Configuration.GetSection(
        nameof(MatchingEngineConfiguration)));

builder.Services.Configure<CorsConfiguration>(
    builder.Configuration.GetSection(
        CorsConfiguration.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Core domain and services
builder.Services.AddSingleton(instrumentCatalogue);

builder.Services.AddSingleton<ITextLogger>(
    CreateTextLogger);

builder.Services.AddSingleton<IMatchingAlgorithm>(
    serviceProvider =>
    {
        var configuration = serviceProvider
            .GetRequiredService<
                IOptions<MatchingEngineConfiguration>>()
            .Value;

        return configuration.Algorithm switch
        {
            MatchingAlgorithmType.Fifo => Fifo.Instance,
            MatchingAlgorithmType.ProRata => ProRata.Instance,
            _ => throw new InvalidOperationException(
                $"Algorithm '{configuration.Algorithm}' is not supported.")
        };
    });

builder.Services.AddSingleton<
    IMatchingEngine,
    MatchingEngine>();

builder.Services.AddSingleton<OrderGateway>();

builder.Services.AddSingleton<
    IExecutionJournal,
    InMemoryExecutionJournal>();

// Market data and transport
builder.Services.AddSingleton<MarketDataHub>();

builder.Services.AddSingleton<
    IMarketDataPublisher,
    WebSocketMarketDataPublisher>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

// Historical replay input
builder.Services.AddSingleton<
    ILobsterInputProvider,
    CsvLobsterInputProvider>();

builder.Services.AddSingleton<
    ILobsterInputProvider,
    ZipLobsterInputProvider>();

builder.Services.AddSingleton<
    IReplayDelay,
    SystemReplayDelay>();

// Selectable market-data sources
builder.Services.AddSingleton<SyntheticMarketSource>();

builder.Services.AddSingleton<
    LobsterReplayMarketSource>();

builder.Services.AddSingleton<LobsterReplayReader>();

builder.Services.AddSingleton<IMarketDataSource>(
    serviceProvider =>
    {
        var configuration = serviceProvider
            .GetRequiredService<
                IOptions<MarketSimulatorConfiguration>>()
            .Value;

        return configuration.Source switch
        {
            MarketDataSourceType.Synthetic =>
                serviceProvider.GetRequiredService<
                    SyntheticMarketSource>(),

            MarketDataSourceType.LobsterReplay =>
                serviceProvider.GetRequiredService<
                    LobsterReplayMarketSource>(),

            _ => throw new InvalidOperationException(
                $"Market-data source " +
                $"'{configuration.Source}' is not supported.")
        };
    });

// Hosted services
builder.Services.AddHostedService<Valkyrie.Core.Valkyrie>();

builder.Services.AddHostedService<
    Valkyrie.Api.Simulation.MarketSimulator>();

var app = builder.Build();

InitializeOrderBooks(app);

app.UseCors(frontendCorsPolicy);

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};

foreach (var allowedOrigin in allowedOrigins)
    webSocketOptions.AllowedOrigins.Add(allowedOrigin);

app.UseWebSockets(webSocketOptions);

// Health endpoint
app.MapGet(
    "/health",
    () => Results.Ok(new
    {
        status = "healthy"
    }));

// Application endpoints
app.MapInstrumentEndpoints();
app.MapOrderEndpoints();
app.MapExecutionEndpoints();
app.MapMarketDataEndpoints();

await app.RunAsync();

public partial class Program;
