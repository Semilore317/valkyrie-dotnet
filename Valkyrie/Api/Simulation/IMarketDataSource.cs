namespace Valkyrie.Api.Simulation;

public interface IMarketDataSource
{
    string Name { get; }
    Task RunAsync(CancellationToken token); // produce order flow until told to stop
}