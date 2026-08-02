namespace Valkyrie.Api.Simulation.Lobster.Input;

public class LobsterInput : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public TextReader MessageReader { get; }
    public TextReader OrderBookReader { get; }

    public LobsterInput(
        TextReader messageReader,
        TextReader orderBookReader,
        IDisposable? owner = null
        )
    {
        MessageReader = messageReader ?? throw new ArgumentNullException(nameof(messageReader));
        OrderBookReader = orderBookReader ?? throw new ArgumentNullException(nameof(orderBookReader));
        _owner = owner;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            MessageReader.Dispose();
        }
        finally
        {
            try
            {
                OrderBookReader.Dispose();
            }
            finally
            {
                _owner?.Dispose();
            }
        }
    }
}