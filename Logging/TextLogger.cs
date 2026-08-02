using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Options;
using Valkyrie.Logging.Configuration;

namespace Valkyrie.Logging;

public class TextLogger : AbstractLogger, ITextLogger
{
    public TextLogger(IOptions<LoggingConfiguration> loggingConfiguration) : base()
    {
        var loggingConfiguration1 = loggingConfiguration.Value ?? throw new ArgumentNullException(nameof(loggingConfiguration));
        var config = loggingConfiguration1.TextLoggerConfiguration ?? throw new InvalidOperationException("TextLoggerConfiguration is missing.");

        // create the directory (if it doesn't exist) and start logging in a file

        string dateDirectoryName = DateTime.Now.ToString("yyyy-MM-dd");
        string targetDirectory = Path.Combine(config.Directory, dateDirectoryName);
        string logFileName = $"{config.FileName}_{DateTime.Now:HH_mm_ss}.{config.FileExtension.TrimStart('.')}";
        string filePath = Path.Combine(targetDirectory, logFileName);

        Directory.CreateDirectory(targetDirectory);

        _ = Task.Run(() => logAsync(filePath, _logQueue, _tokenSource.Token));
    }

    private static async Task logAsync(string filepath, BufferBlock<LogInfo> logQueue, CancellationToken token)
    {
        // using tells C# to close these when the operations end
        // important since they open direct connections to the OS's file system
        using var fileStream = new FileStream(filepath, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var streamWriter = new StreamWriter(fileStream);
        streamWriter.AutoFlush = true; // to clear the output buffer

        try
        {
            while (true)
            {
                var logItem = await logQueue.ReceiveAsync(token).ConfigureAwait(false);
                string formattedMessage = FormatLogItem(logItem);
                await streamWriter.WriteLineAsync(formattedMessage).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // catches when the token is cancelled on shutdown
        }
        finally
        {
            //drain any remaining logs posted to the same queue before shutdown
            while (logQueue.TryReceive(out var logItem))
            {
                string formattedMessage = FormatLogItem(logItem);
                streamWriter.WriteLine(formattedMessage);
            }
        }
    }

    private static string FormatLogItem(LogInfo logItem)
    {
        return $"[{logItem.Now:yyyy-MM-dd HH-mm-ss.fffffff}] [{logItem.ThreadName,-30}:{logItem.ThreadId:000}"
               + $"[{logItem.LogLevel}] {logItem.Message}";
    }

    protected override void Log(LogLevel logLevel, string module, string message)
    {
        _logQueue.Post(new LogInfo(logLevel, module, message, DateTime.Now, Thread.CurrentThread.ManagedThreadId,
            Thread.CurrentThread.Name!));
    }

    // Finalizer Queue.... kinda like a destructor in C++ but not realaly
    ~TextLogger()
    {
        //destroy everything that hasn't been GC'd and is available for destruction
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
        }

        lock (_lock)
        {
            _disposed = true;
        }


        if (disposing)
        {
            // remove managed resources(internal stuff)
            _tokenSource.Cancel();
            _tokenSource.Dispose();
        }


        // remove unmanaged resources(external stuff) e.g db, filestream atc
    }

    /// <summary>
    /// thread-safe queue with an async api
    /// </summary>
    private readonly BufferBlock<LogInfo> _logQueue = new BufferBlock<LogInfo>();


    private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
    private bool _disposed = false;
    private readonly object _lock = new object();
}