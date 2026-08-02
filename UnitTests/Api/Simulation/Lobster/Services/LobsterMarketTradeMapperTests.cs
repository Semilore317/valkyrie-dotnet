using FluentAssertions;
using Valkyrie.Api.Simulation.Lobster.Models;
using Valkyrie.Api.Simulation.Lobster.Services;
using Valkyrie.MatchingEngine;
using Valkyrie.Orders;

namespace UnitTests.Api.Simulation.Lobster.Services;

public class LobsterMarketTradeMapperTests
{
    private static readonly DateTimeOffset HistoricalTimeStamp
        = new(2012, 6, 21, 9, 30, 0, TimeSpan.FromHours(-4));


    [Theory]
    [InlineData(LobsterEventType.VisibleExecution, LobsterDirection.Sell, Side.Buy)]
    [InlineData(LobsterEventType.HiddenExecution, LobsterDirection.Buy, Side.Sell)]
    public void Map_MapsExecutionMetadata(
        LobsterEventType eventType,
        LobsterDirection direction,
        Side expectedAggressorSide
    )
    {
        var frame = CreateFrame(eventType, direction);
        var trade = LobsterMarketTradeMapper.Map(frame);

        trade.Should().NotBeNull();
        trade!.SecurityId.Should().Be(2);
        trade.Price.Should().Be(58_543.24m);
        trade.Quantity.Should().Be(40u);
        trade.OccurredAt.Should().Be(HistoricalTimeStamp);
        trade.AggressorSide.Should().Be(expectedAggressorSide);
    }

    [Theory]
    [InlineData(LobsterEventType.Submission)]
    [InlineData(LobsterEventType.PartialCancellation)]
    [InlineData(LobsterEventType.Deletion)]
    [InlineData(LobsterEventType.CrossTrade)]
    [InlineData(LobsterEventType.TradingHalt)]
    public void Map_IgnoresNonExecutionEvents(LobsterEventType eventType)
    {
        var frame = CreateFrame(eventType, LobsterDirection.Sell);
        var trade = LobsterMarketTradeMapper.Map(frame);
        trade.Should().BeNull();

    }

    private static LobsterReplayFrame CreateFrame(
        LobsterEventType eventType,
        LobsterDirection direction
    )
    {
        var message = new LobsterMessage(
            SecondsAfterMidnight: 34_200m,
            EventType: eventType,
            OrderId: 1234,
            Size: 40,
            RawPrice: 5_854_324,
            Direction: direction
        );

        var snapshot = new OrderBookSnapshot(
            SecurityId: 2,
            Bid: 67_670,
            Ask: 67_680,
            Spread: 10,
            Bids: [],
            Asks: []
        );

        return new LobsterReplayFrame(
            LineNumber: 17,
            HistoricalTimeStamp: HistoricalTimeStamp,
            Message: message,
            Snapshot: snapshot
        );
    }
}