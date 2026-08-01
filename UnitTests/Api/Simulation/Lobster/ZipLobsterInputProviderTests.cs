using System.IO.Compression;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Api.Simulation.Lobster.Input;
using Valkyrie.Core.Configuration;

namespace UnitTests.Api.Simulation.Lobster;

public sealed class ZipLobsterInputProviderTests
{
    private const string MessageEntryName = "data/AAPL_2012-06-21_34200000_57600000_message_10.csv";
    private const string OrderBookEntryName = "data/AAPL_2012-06-21_34200000_57600000_orderbook_10.csv";

    [Fact]
    public void Open_ReturnsBothReaders()
    {
        using var directory = new TemporaryDirectory();

        directory.CreateArchive(
            "sample.zip",
            (MessageEntryName, "message-row"),
            (OrderBookEntryName, "orderbook-entry"));

        var provider = CreateProvider(directory.DirectoryPath);

        using var input = provider.Open(CreateInstrument());

        input.MessageReader.ReadLine()
            .Should().Be("message-row");
        input.OrderBookReader.ReadLine()
            .Should().Be("orderbook-entry");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Open_RejectsMissingRequiredEntry(bool messageIsMissing)
    {
        using var directory = new TemporaryDirectory();

        string existingEntry;
        string existingContents;
        string missingSuffix;

        if (messageIsMissing)
        {
            existingEntry = OrderBookEntryName;
            existingContents = "orderbook-row";
            missingSuffix = "_message_10.csv";
        }
        else
        {
            existingEntry = MessageEntryName;
            existingContents = "message-row";
            missingSuffix = "_orderbook_10.csv";
        }

        directory.CreateArchive("sample.zip", (existingEntry, existingContents));

        var provider = CreateProvider(directory.DirectoryPath);

        var open = () =>
        {
            using var input = provider.Open(CreateInstrument());
        };

        open.Should()
            .Throw<InvalidDataException>()
            .WithMessage($"*{missingSuffix}*");
    }

    [Fact]
    public void Open_RejectsMismatchedDatasets()
    {
        using var directory = new TemporaryDirectory();

        const string mismatchedOrderBookEntry = "data/MSFT_2012-06-21_34200000_57600000_orderbook_10.csv";

        directory.CreateArchive(
            "sample.zip",
            (MessageEntryName, "message-row"),
            (mismatchedOrderBookEntry, "orderbook-entry"));

        var provider = CreateProvider(directory.DirectoryPath);

        Action open = () =>
        {
            using var input = provider.Open(CreateInstrument());
        };

        open.Should()
            .Throw<InvalidDataException>()
            .WithMessage("*same LOBSTER dataset*");
    }

    [Theory]
    [InlineData("MSFT", 2012, 6, 21)]
    [InlineData("AAPL", 2012, 6, 22)]
    public void Open_RejectsDatasetThatDoesNotMatchConfiguredInstrument(
        string configuredSymbol,
        int year,
        int month,
        int day
    )
    {
        using var directory = new TemporaryDirectory();
        directory.CreateArchive(
            "sample.zip",
            (MessageEntryName, "message-row"),
            (OrderBookEntryName, "orderbook-row")
            );
        
        var provider = CreateProvider(directory.DirectoryPath);
        var instrument = CreateInstrument();

        instrument.Symbol = configuredSymbol;
        instrument.SessionMidnight = new DateTimeOffset(
            year, month, day, 0, 0, 0, TimeSpan.FromHours(-4));

        Action open = () =>
        {
            using var input = provider.Open(instrument);
        };
        
        open.Should()
            .Throw<InvalidDataException>()
        .WithMessage($"*does not match configured symbol*session date*");

    }

    private static ZipLobsterInputProvider CreateProvider(string contentRoot)
    {
        return new ZipLobsterInputProvider(new TestHostEnvironment(contentRoot));
    }

    private static HistoricalReplayInstrument CreateInstrument()
    {
        return new HistoricalReplayInstrument
        {
            SecurityId = 2,
            Symbol = "AAPL",
            DataFormat = LobsterDataFormat.ZipArchive,
            DataPath = "sample.zip",
            SessionMidnight = new DateTimeOffset(
                2012, 6, 21
                , 0, 0, 0, TimeSpan.FromHours(-4)),
            BookDepth = 10
        };
    }

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "UnitTests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Testing";
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(
            Path.GetTempPath(), $"lobster-zip-tests-{Guid.NewGuid()}");

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        public void CreateArchive(
            string fileName,
            params (string EntryName, string Contents)[] entries
            //params allows you to pass a comma-separated list of values into the method instead of
            //making an array manually.
        )
        {
            var archivePath = Path.Combine(DirectoryPath, fileName);
            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

            foreach (var entryData in entries)
            {
                var entry = archive.CreateEntry(entryData.EntryName);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(entryData.Contents);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}