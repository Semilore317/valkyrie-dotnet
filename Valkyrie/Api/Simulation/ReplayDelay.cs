namespace Valkyrie.Api.Simulation;

public interface IReplayDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken token);
}

public sealed class SystemReplayDelay : IReplayDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken token)
    {
        return Task.Delay(delay, token);
    }
}