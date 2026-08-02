using System.Globalization;
using Valkyrie.MatchingEngine;

namespace Valkyrie.Api.Simulation.Lobster.Models;

public static class LobsterOrderBookParser
{
    public static OrderBookSnapshot Parse(
        string line,
        long lineNumber,
        long securityId,
        int expectedLevels)
    {
        var fields = line.Split(',');
        var expectedFields = expectedLevels * 4;

        if (fields.Length != expectedFields)
            throw new InvalidDataException(
                $"Book row {lineNumber} has {fields.Length} fields but expected {expectedFields}."
            );

        var asks = new List<Level>(expectedLevels);
        var bids = new List<Level>(expectedLevels);

        try
        {
            for (var index = 0; index < fields.Length; index += 4)
            {
                var askRaw = long.Parse(
                    fields[index],
                    CultureInfo.InvariantCulture);

                var askQuantity = long.Parse(
                    fields[index + 1],
                    CultureInfo.InvariantCulture);

                var bidRaw = long.Parse(
                    fields[index + 2],
                    CultureInfo.InvariantCulture);

                var bidQuantity = long.Parse(
                    fields[index + 3],
                    CultureInfo.InvariantCulture);

                // Zero Quantity means the level
                if (askQuantity > 0)
                    asks.Add(new Level(
                        ToVisibleBookCents(askRaw),
                        askQuantity));

                if (bidQuantity > 0)
                    bids.Add(new Level(
                        ToVisibleBookCents(bidRaw),
                        bidQuantity));
            }
        }
        catch (Exception exception)
            when (exception is FormatException or OverflowException)
        {
            throw new InvalidDataException($"Invalid Order at order-book row {lineNumber}", exception);
        }

        ValidateOrdering(asks, bids, lineNumber);

        long? bestAsk = asks.Count == 0 ? null : asks[0].Price;
        long? bestBid = bids.Count == 0 ? null : bids[0].Price;

        long? spread = bestAsk.HasValue && bestBid.HasValue
            ? bestAsk.Value - bestBid.Value
            : null;

        return new OrderBookSnapshot(
            securityId,
            bestBid,
            bestAsk,
            spread,
            bids,
            asks
        );
    }

    private static long ToVisibleBookCents(long rawPrice)
    {
        if (rawPrice < 0 || rawPrice % 100 != 0)
            throw new InvalidDataException(($"Visible LOBSTER price {rawPrice} is not an exact cent price"));

        return rawPrice / 100;
    }

    private static void ValidateOrdering
    (
        IReadOnlyList<Level> asks,
        IReadOnlyList<Level> bids,
        long lineNumber)
    {
        for (var index = 1; index < asks.Count; index++)
            if (asks[index].Price <= asks[index - 1].Price)
                throw new InvalidDataException($"Asks are not ascending at row {lineNumber}");

        for (var index = 1; index < bids.Count; index++)
        {
            if (bids[index].Price >= bids[index - 1].Price)
                throw new InvalidDataException($"Bids are not descending at row {lineNumber}");
        }
    }
}