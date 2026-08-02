using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Api.Simulation.Lobster.Input;
using Valkyrie.Core.Configuration;

namespace UnitTests.Api.Simulation.Lobster;

public sealed class CsvLobsterInputProviderTests
{
    [Fact]
    public void Open_ReturnsBothReaders()
    {
        using var directory = new TemporaryDirectory();

        directory.Write("AAPL_2012-06-21_34200000_57600000_message_10.csv", "message-row");
        directory.Write("AAPL_2012-06-21_34200000_57600000_orderbook_10.csv", "orderbook-row");

        var provider = CreateProvider(directory.DirectoryPath);
        using var input = provider.Open(CreateInstrument());

        input.MessageReader.ReadLine().Should().Be("message-row");
        input.OrderBookReader.ReadLine().Should().Be("orderbook-row");
    }

    [Theory]
    [InlineData("MSFT", 2012, 6, 21)]
    [InlineData("AAPL", 2012, 6, 22)]
    public void Open_RejectsDatasetThatDoesNotMatchConfiguredInstrument(
        string instrumentName,
        int year,
        int month,
        int day)
    {
        using var directory = new TemporaryDirectory();

        directory.Write("AAPL_2012-06-21_34200000_57600000_message_10.csv", "message-row");
        directory.Write("AAPL_2012-06-21_34200000_57600000_orderbook_10.csv", "orderbook-row");

        var provider = CreateProvider(directory.DirectoryPath);
        var instrument = CreateInstrument();

        instrument.Ticker = instrumentName;
        instrument.SessionMidnight = new DateTimeOffset(
            year, month, day, 0, 0, 0, TimeSpan.FromHours(-4));

        Action open = () =>
        {
            using var input = provider.Open(instrument);
        };
        open.Should()
            .Throw<InvalidDataException>()
            .WithMessage("*does not match configured ticker*session date*");
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
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"lobster-tests-{Guid.NewGuid()}");

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        public void Write(string filename, string contents)
        {
            File.WriteAllText(Path.Combine(DirectoryPath, filename), contents);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
                Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    private static CsvLobsterInputProvider CreateProvider(string contentRoot)
    {
        return new CsvLobsterInputProvider(new TestHostEnvironment(contentRoot));
    }

    private static HistoricalReplayInstrument CreateInstrument()
    {
        return new HistoricalReplayInstrument
        {
            SecurityId = 2,
            Ticker = "AAPL",
            DataFormat = LobsterDataFormat.CsvDirectory,
            DataPath = ".",
            SessionMidnight = new DateTimeOffset(
                2012,
                6,
                21,
                0,
                0,
                0,
                TimeSpan.FromHours(-4)),
            BookDepth = 10
        };
    }
}