namespace Valkyrie.Logging;

public record LogInfo(
    LogLevel LogLevel,
    string Module,
    string Message,
    DateTime Now,
    int ThreadId,
    string ThreadName
);