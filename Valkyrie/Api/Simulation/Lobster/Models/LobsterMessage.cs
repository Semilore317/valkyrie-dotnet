using System.Globalization;

namespace Valkyrie.Api.Simulation.Lobster.Models;

/// <summary>
/// represents an immutable row from a LOBSTER message file 
/// </summary>
public record LobsterMessage(
    decimal SecondsAfterMidnight,
    LobsterEventType EventType,
    long OrderId,
    uint Size,
    long RawPrice,
    LobsterDirection Direction
)
{
    public bool IsExecution =>
        EventType is LobsterEventType.VisibleExecution or LobsterEventType.HiddenExecution;

    // cent units but decimal since hidden executions may be sub-cent
    // 243444 -> 24344.44 cents --> $24.3444

    public decimal PriceInCents => RawPrice / 100m;

    public static LobsterMessage Parse(string line, long lineNumber)
    {
        var fields = line.Split(',');

        if (fields.Length != 6)
            throw new InvalidDataException($"Message Row {lineNumber} has {fields.Length}  fields; expected 6");

        try
        {
            var eventType = (LobsterEventType)int.Parse(
                fields[1],
                CultureInfo.InvariantCulture); // uses US-style formatting always

            if (!Enum.IsDefined(eventType))
                throw new InvalidDataException($"Unsupported event type {fields[1]} at row  {lineNumber}");

            var direction = (LobsterDirection)int.Parse(
                fields[5],
                CultureInfo.InvariantCulture);

            if (direction is not LobsterDirection.Buy and not LobsterDirection.Sell)
                throw new InvalidDataException(
                    $"Unsupported direction {fields[5]} at row {lineNumber}");

            return new LobsterMessage(
                decimal.Parse(fields[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture),
                eventType,
                long.Parse(fields[2], CultureInfo.InvariantCulture),
                uint.Parse(fields[3], CultureInfo.InvariantCulture),
                long.Parse(fields[4], CultureInfo.InvariantCulture),
                direction
            );
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new InvalidDataException(
                $"Invalid numeric value at message row {lineNumber}. ", exception 
            );
        }
    }
}