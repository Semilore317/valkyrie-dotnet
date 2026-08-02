namespace Valkyrie.MatchingEngine;

public class Fill
{
    public long SecurityId { get; set; }

    public long BidOrderId { get; set; }
    public long AskOrderId { get; set; }

    public long ExecutionPrice { get; set; } // in cents

    public uint FilledQuantity { get; set; } // min(bid.CurrentQuantity, ask.CurrentQuantity)
    public DateTime FilledAt { get; set; } // Datetime.UtcNow
}