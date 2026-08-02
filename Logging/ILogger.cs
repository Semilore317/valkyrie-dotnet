using System.Diagnostics;

namespace Valkyrie.Logging;

public interface ILogger
{
    void Debug(string module, string message);
    void Debug(string module, Exception exception);

    void Info(string module, string message);
    void Info(string module, Exception exception);

    void Warning(string module, string message);
    void Warning(string module, Exception exception);

    void Error(string module, string message);
    void Error(string module, Exception exception);
}