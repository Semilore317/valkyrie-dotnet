using FluentAssertions;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Api.Simulation.Lobster.Input;
using Valkyrie.Api.Simulation.Lobster.Models;
using Valkyrie.Api.Simulation.Lobster.Services;
using Valkyrie.Core.Configuration;

namespace UnitTests.Api.Simulation.Lobster.Services;

public sealed class LobsterReplayReaderTests
{
    private static readonly DateTimeOffset SessionMidnight = new(2012, 6, 21, 0, 0, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task ReadAsync_YieldsPairedFrames()
    {
        var provider = new StubInputProvider(
            messageRows:
            [
                "34200,1,1001,50,100100,1",
                "34200.25,4,1001,20,100200,-1"
            ],
            orderbookRows:
            [
                "100200,100,100100,200," + "100300,300,100000,400",
                "100300,90,100200,180," + "100400,270,100100,360"
            ]
        );

        var reader = new LobsterReplayReader([provider]);
        var frames = await ReadAllAsync(reader, CreateInstrument());

        frames.Should().HaveCount(2);
        frames[0].LineNumber.Should().Be(1);
        frames[0].HistoricalTimeStamp.Should().Be(SessionMidnight.AddHours(9).AddMinutes(30));
        frames[0].Message.EventType.Should().Be(LobsterEventType.Submission);
        frames[0].Snapshot.SecurityId.Should().Be(2);
        frames[0].Snapshot.Bid.Should().Be(1001);
        frames[0].Snapshot.Ask.Should().Be(1002);
        frames[0].Snapshot.Spread.Should().Be(1);
        frames[1].HistoricalTimeStamp.Should().Be(SessionMidnight.AddHours(9).AddMinutes(30).AddMilliseconds(250));
        frames[1].Message.EventType.Should().Be(LobsterEventType.VisibleExecution);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAsync_RejectsDifferentRowCounts(
        bool messageHasExtraRow
    )
    {
        string[] oneMessage = ["34200,1,1001,50,100100,1"];
        string[] twoMessages =
        [
            "34200,1,1001,50,100100,1",
            "34200.25,4,1001,20,100200,-1"
        ];

        string[] oneBook = ["100200,100,100100,200," + "100300,300,100000,400"];
        string[] twoBooks =
        [
            "100200,100,100100,200," + "100300,300,100000,400",
            "100300,90,100200,180," + "100400,270,100100,360"
        ];

        var provider = new StubInputProvider(
            messageHasExtraRow
                ? twoMessages
                : oneMessage,
            messageHasExtraRow
                ? oneBook
                : twoBooks
        );

        var reader = new LobsterReplayReader([provider]);

        Func<Task> action = () => DrainAsync(reader, CreateInstrument());

        var assertion = await action.Should().ThrowAsync<InvalidDataException>();

        assertion.WithMessage("*row 2*");
    }

    [Fact]
    public async Task ReadAsync_RejectsMissingProvider()
    {
        var csvProvider = new StubInputProvider(
            messageRows: [],
            orderbookRows: []);

        var reader = new LobsterReplayReader([csvProvider]);

        var instrument = CreateInstrument();

        instrument.DataFormat = LobsterDataFormat.ZipArchive;

        Func<Task> action = () => DrainAsync(reader, instrument);

        var assertion = await action.Should()
            .ThrowAsync<InvalidOperationException>();
        
        assertion.WithMessage("*ZipArchive*");
    }

    [Fact]
    public async Task ReadAsync_RejectsBackwardsTimestamp()
    {
        var provider = new StubInputProvider(
            messageRows:[
                "34200.25,1,1001,50,100100,1",
                "34200,4,1001,20,100200,-1"
            ],
            orderbookRows:[
                "100200,100,100100,200,"+ "100300,300,100000,400",
                "100300,90,100200,180," + "100400,270,100100,360"
            ]
            );

        var reader = new LobsterReplayReader([provider]);

        var action = () => DrainAsync(reader, CreateInstrument());

        var assertion = await action.Should()
            .ThrowAsync<InvalidDataException>();

        assertion.WithMessage("*timestamp moved backwards*");
    }

    private static async Task<List<LobsterReplayFrame>> ReadAllAsync(
        LobsterReplayReader reader,
        HistoricalReplayInstrument instrument
    )
    {
        var frames = new List<LobsterReplayFrame>();

        await foreach (var frame in reader.ReadAsync(instrument))
            frames.Add(frame);

        return frames;
    }

    private static async Task DrainAsync(
        LobsterReplayReader reader,
        HistoricalReplayInstrument instrument
    )
    {
        await foreach (var _ in reader.ReadAsync(instrument))
        {
        }
    }

    private static HistoricalReplayInstrument CreateInstrument()
    {
        return new HistoricalReplayInstrument
        {
            SecurityId = 2,
            Ticker = "AAPL",
            DataFormat = LobsterDataFormat.CsvDirectory,
            DataPath = ".",
            SessionMidnight = SessionMidnight,
            BookDepth = 2
        };
    }

    private sealed class StubInputProvider(
        IReadOnlyList<string> messageRows,
        IReadOnlyList<string> orderbookRows
    ) : ILobsterInputProvider
    {
        public LobsterDataFormat Format => LobsterDataFormat.CsvDirectory;

        public LobsterInput Open(HistoricalReplayInstrument instrument)
        {
            var messageText = string.Join(Environment.NewLine, messageRows);
            var orderBookText = string.Join(Environment.NewLine, orderbookRows);

            return new LobsterInput(
                new StringReader(messageText),
                new StringReader(orderBookText)
            );
        }
    }
}