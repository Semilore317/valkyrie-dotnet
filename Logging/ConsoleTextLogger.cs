namespace Valkyrie.Logging;

public sealed class ConsoleTextLogger : AbstractLogger, ITextLogger
{
    protected override void Log(LogLevel logLevel, string module, string message)
    {
        var thread = Thread.CurrentThread;
        var threadName = thread.Name ?? "unnamed";

        Console.WriteLine(
            $"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.ffffff zzz}] " +
            $"[{threadName,-30}:{thread.ManagedThreadId:000}] " +
            $"[{logLevel}] [{module}] {message}");
    }

    public void Dispose()
    {
        // Console output does not own disposable resources.
    }
}
