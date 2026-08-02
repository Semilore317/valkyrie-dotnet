using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Core.Configuration;

namespace Valkyrie.Api.Simulation.Lobster.Input;

public sealed class CsvLobsterInputProvider(
    IHostEnvironment environment
) : ILobsterInputProvider
{
    public LobsterDataFormat Format => LobsterDataFormat.CsvDirectory;

    public LobsterInput Open(HistoricalReplayInstrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        if (instrument.DataFormat != Format)
            throw new InvalidOperationException(
                $"{nameof(CsvLobsterInputProvider)} cannot open" +
                $"the `{instrument.DataFormat}` format.");


        if (instrument.BookDepth <= 0)
            throw new InvalidOperationException("LOBSTER Book Depth must be greater than zero");

        var directory = ResolveDirectory(instrument.DataPath);

        var messageSuffix = $"_message_{instrument.BookDepth}.csv";

        var orderBookSuffix = $"_orderbook_{instrument.BookDepth}.csv";

        var messagePath = FindSingleFile(directory, messageSuffix);

        var orderBookPath = FindSingleFile(directory, orderBookSuffix);

        ValidateMatchingDataset(instrument, messagePath, messageSuffix, orderBookPath, orderBookSuffix);

        var messageReader = OpenReader(messagePath);

        try
        {
            var orderBookReader = OpenReader(orderBookPath);

            return new LobsterInput(messageReader, orderBookReader);
        }
        catch
        {
            messageReader.Dispose();
            throw;
        }
    }

    private string ResolveDirectory(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException("LOBSTER Data path cannot be empty");

        var expandedPath = Environment.ExpandEnvironmentVariables(configuredPath.Trim());

        var combinedPath = Path.IsPathRooted(expandedPath)
            ? expandedPath
            : Path.Combine(environment.ContentRootPath, expandedPath);

        var fullPath = Path.GetFullPath(combinedPath);

        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"LOBSTER data directory {fullPath} was not found");

        return fullPath;
    }

    private static string FindSingleFile(string directory, string requiredSuffix)
    {
        var matchingFiles = Directory.EnumerateFiles(
                directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).EndsWith(
                requiredSuffix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matchingFiles.Length switch
        {
            1 => matchingFiles[0],
            0 => throw new InvalidDataException(
                $"Directory does not contain a " +
                $"'{requiredSuffix}' file."
            ),
            _ => throw new InvalidDataException(
                $"Directory contains multiple " +
                $"'{requiredSuffix}' files.")
        };
    }

    private static void ValidateMatchingDataset(
        HistoricalReplayInstrument instrument,
        string messagePath,
        string messageSuffix,
        string orderBookPath,
        string orderBookSuffix
    )
    {
        var messageName = Path.GetFileName(messagePath);
        var orderBookName = Path.GetFileName(orderBookPath);
        
        var messagePrefix = messageName.Substring(0, messageName.Length - messageSuffix.Length);
        var orderBookPrefix = orderBookName.Substring(0, orderBookName.Length - orderBookSuffix.Length);

        if (!string.Equals(messagePrefix, orderBookPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "The message and order-book files do not " +
                "belong to the same LOBSTER dataset"
            );
        
        LobsterDatasetIdentityValidator.Validate(instrument, messagePrefix);
    }

    private static StreamReader OpenReader(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options:
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return new StreamReader(stream);
    }
}