using System.IO.Compression;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Core.Configuration;

namespace Valkyrie.Api.Simulation.Lobster.Input;

public class ZipLobsterInputProvider(
    IHostEnvironment environment
) : ILobsterInputProvider
{
    public LobsterDataFormat Format => LobsterDataFormat.ZipArchive;

    public LobsterInput Open(HistoricalReplayInstrument instrument)
    {
        ArgumentNullException.ThrowIfNull(instrument);

        if (instrument.DataFormat != Format)
            throw new InvalidOperationException($"{nameof(LobsterDataFormat)} cannot open" +
                                                $"the {instrument.DataFormat} format.");

        if (instrument.BookDepth <= 0)
            throw new InvalidOperationException($"Book Depth must be greater than zero");

        var archivePath = ResolveArchivePath(instrument.DataPath);
        var archive = OpenArchive(archivePath);

        TextReader? messageReader = null;
        TextReader? orderBookReader = null;
        
        try
        {
            var messageSuffix = $"_message_{instrument.BookDepth}.csv";
            var orderBookSuffix = $"_orderbook_{instrument.BookDepth}.csv";
            var messageEntry = FindSingleEntry(archive, messageSuffix);
            var orderBookEntry = FindSingleEntry(archive, orderBookSuffix);
            ValidateMatchingDataset(messageEntry, messageSuffix, orderBookEntry, orderBookSuffix);

            messageReader = OpenReader(messageEntry);
            orderBookReader = OpenReader(orderBookEntry);
            
            return new LobsterInput(messageReader, orderBookReader, archive);
        }
        catch
        {
            messageReader?.Dispose();
            orderBookReader?.Dispose();
            archive.Dispose();
            throw;
        }
    }

    private string ResolveArchivePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            throw new InvalidOperationException("Data Path Cannot be Empty");

        var expandedPath = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
        var combinedPath = Path.IsPathRooted(expandedPath)
            ? expandedPath
            : Path.Combine(environment.ContentRootPath, expandedPath);

        var fullPath = Path.GetFullPath(combinedPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"LOBSTER ZIP Archive `{fullPath}` was not found", fullPath);

        return fullPath;
    }

    private static ZipArchive OpenArchive(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous);

        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static ZipArchiveEntry FindSingleEntry(
        ZipArchive archive,
        string requiredSuffix
    )
    {
        var matchingEntries = archive.Entries
            .Where(entry => !string.IsNullOrEmpty((entry.Name)))
            .Where(entry => entry.FullName.EndsWith(requiredSuffix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matchingEntries.Length switch
        {
            1 => matchingEntries[0],
            0 => throw new InvalidDataException($"Archive does not contain a `{requiredSuffix}` entry."),
            _ => throw new InvalidDataException($"Archive contains multiple `{requiredSuffix}` entries")
        };
    }

    private static void ValidateMatchingDataset(
        ZipArchiveEntry messageEntry,
        string messageSuffix,
        ZipArchiveEntry orderBookEntry,
        string orderBookSuffix
    )
    {
        var messageName = messageEntry.Name;
        var orderBookName = orderBookEntry.Name;

        var messagePrefix = messageName.Substring(0,
            messageName.Length - messageSuffix.Length);
        var orderBookPrefix = orderBookName.Substring(0,
            orderBookName.Length - orderBookSuffix.Length);

        if (!string.Equals(messagePrefix, orderBookPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The message and order-book entries do not belong to the same LOBSTER dataset");
    }

    private static StreamReader OpenReader(ZipArchiveEntry entry)
    {
        return new StreamReader(entry.Open());
    }
}