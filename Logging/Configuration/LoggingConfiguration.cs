namespace Valkyrie.Logging.Configuration;

public class LoggingConfiguration
{
    // the .net options binder creates config objects through public parameterless constructors
    // i removed the previous parameterized constructor so that the compiler generates a parameterless one
    public LoggerType LoggerType { get; set; }
    public TextLoggerConfiguration? TextLoggerConfiguration { get; set; }

    // i can define others later for db etc, console, trace etc...
}

public class TextLoggerConfiguration
{
    public string Directory { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
}
