using FluentAssertions;
using Microsoft.Extensions.Options;
using Valkyrie.Api.Dto;
using Valkyrie.Api.MarketData;
using Valkyrie.Api.Simulation;
using Valkyrie.Api.Simulation.Lobster.Enums;
using Valkyrie.Api.Simulation.Lobster.Input;
using Valkyrie.Api.Simulation.Lobster.Services;
using Valkyrie.Core.Configuration;
using Valkyrie.MatchingEngine;

namespace UnitTests.Api.Simulation.Lobster.Services;

public sealed class LobsterReplayReaderMarketSourceTests
{
    private static readonly DateTimeOffset SessionMidnight
        = new(2012, 6, 21, 0, 0, 0, 0, TimeSpan.FromHours(-4));

    [Fact]
    public async Task RunAsync_CoalescesFramesAndAppliesPlaybackSpeed()
    {
        var provider = new StubInputProvider(
            messageRows:
            [
                "34200,1,1001,50,100100,1",
                "34200.1,2,1001,40,100100,1",
                "34200.5,3,1001,40,100100,1"
            ],
            orderBookRows:
            [
                "100200,100,100100,200",
                "100300,90,100200,180",
                "100400,80,100300,160"
            ]);

        var publisher = new CapturingPublisher();
        var replayDelay = new RecordingReplayDelay();

        var configuration = CreateConfiguration(
            playbackSpeed: 2,
            maxUpdatesPerSecond: 5
        );

        var source = CreateSource(provider, publisher, replayDelay, configuration);

        await source.RunAsync(CancellationToken.None);

        publisher.BookSnapshots.Should().HaveCount(2);
        publisher.BookSnapshots[0].Bid.Should().Be(1001);
        publisher.BookSnapshots[1].Bid.Should().Be(1003);

        replayDelay.Delays.Should().ContainSingle();
        replayDelay.Delays[0].Should().Be(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task RunAsync_FlushesFinalPendingSnapshots()
    {
        var provier = new StubInputProvider(
            messageRows:
            [
                "34200,1,1001,50,100100,1",
                "34200.05,2,1001,40,100100,1"
            ],
            orderBookRows:
            [
                "100200,100,100100,200",
                "100300,90,100200,180"
            ]);

        var publisher = new CapturingPublisher();
        var replayDelay = new RecordingReplayDelay();

        var configuration = CreateConfiguration(
            playbackSpeed: 1,
            maxUpdatesPerSecond: 5);
    }

    [Fact]
    public async Task RunAsync_LoopsAndReopensInputUntilCancelled()
    {
        var provider = CreateSingleFrameProvider();
        using var cancellation = new CancellationTokenSource();

        var publisher = new CapturingPublisher(publicationCount =>
        {
            if (publicationCount == 2)
                cancellation.Cancel();
        });

        var replayDelay = new RecordingReplayDelay();

        var configuration = CreateConfiguration(
            playbackSpeed: 1,
            maxUpdatesPerSecond: 5,
            loop: true
        );
        var source = CreateSource(provider, publisher, replayDelay, configuration);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task RunAsync_RejectsInvalidPlaybackSpeed(double playbackSpeed)
    {
        var source = CreateSource(
            CreateSingleFrameProvider(),
            new CapturingPublisher(),
            new RecordingReplayDelay(),
            CreateConfiguration(playbackSpeed, maxUpdatesPerSecond: 5));

        var action = () =>
            source.RunAsync(CancellationToken.None);

        var assertion = await action.Should()
            .ThrowAsync<InvalidOperationException>();

        assertion.Which.Message.Should().Contain("playback speed");
        assertion.Which.Message.Should().Contain("finite and greater than zero");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RunAsync_RejectsInvalidUpdateRate(int maxUpdatesPerSecond)
    {
        var source = CreateSource(
            CreateSingleFrameProvider(),
            new CapturingPublisher(),
            new RecordingReplayDelay(),
            CreateConfiguration(playbackSpeed: 1, maxUpdatesPerSecond)
        );

        var action = () => source.RunAsync(CancellationToken.None);
        var assertion = await action.Should().ThrowAsync<InvalidOperationException>();
        assertion.Which.Message.Should().Contain("update");
        assertion.Which.Message.Should().Contain("greater than zero");
    }

    
    private static LobsterReplayMarketSource CreateSource(
        StubInputProvider provider,
        CapturingPublisher publisher,
        RecordingReplayDelay replayDelay,
        MarketSimulatorConfiguration configuration
    )
    {
        var reader = new LobsterReplayReader([provider]);

        return new LobsterReplayMarketSource(reader, publisher, Options.Create(configuration), replayDelay);
    }

    private sealed class RecordingReplayDelay : IReplayDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Delays.Add(delay);

            return Task.CompletedTask;
        }
    }

    private static MarketSimulatorConfiguration CreateConfiguration(
        double playbackSpeed,
        int maxUpdatesPerSecond,
        bool loop = false
    )
    {
        return new MarketSimulatorConfiguration
        {
            HistoricalReplay = new HistoricalReplayConfiguration
            {
                PlaybackSpeed = playbackSpeed,
                MaxBookUpdatesPerSecond = maxUpdatesPerSecond,
                Loop = loop,
                Instruments = [CreateInstrument()]
            }
        };
    }

    private static HistoricalReplayInstrument CreateInstrument()
    {
        return new HistoricalReplayInstrument
        {
            SecurityId = 2,
            Symbol = "AAPL",
            DataFormat = LobsterDataFormat.CsvDirectory,
            DataPath = ".",
            SessionMidnight = SessionMidnight,
            BookDepth = 1
        };
    }

    private static StubInputProvider CreateSingleFrameProvider()
    {
        return new StubInputProvider(
            messageRows: ["34200,1,1001,50,100100,1"],
            orderBookRows: ["100200,100,100100,200"]
        );
    }

    private sealed class CapturingPublisher(
        Action<int>? onBookPublished = null
    ) : IMarketDataPublisher
    {
        public List<OrderBookSnapshot> BookSnapshots { get; } = [];

        public void PublishTrade(TradeEvent tradeEvent)
        {
        }

        public void PublishBook(OrderBookSnapshot bookSnapshot)
        {
            BookSnapshots.Add(bookSnapshot);

            onBookPublished?.Invoke(BookSnapshots.Count);
        }
    }

    private sealed class StubInputProvider(
        IReadOnlyList<string> messageRows,
        IReadOnlyList<string> orderBookRows) : ILobsterInputProvider
    {
        public int OpenCount { get; private set; }
        public LobsterDataFormat Format => LobsterDataFormat.CsvDirectory;

        public LobsterInput Open(HistoricalReplayInstrument instrument)
        {
            OpenCount++;

            var messageText = string.Join(Environment.NewLine, messageRows);
            var orderBookText = string.Join(Environment.NewLine, orderBookRows);

            return new LobsterInput(
                new StringReader(messageText),
                new StringReader(orderBookText)
            );
        }
    }
}