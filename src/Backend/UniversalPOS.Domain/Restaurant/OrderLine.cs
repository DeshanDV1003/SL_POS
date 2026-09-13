namespace UniversalPOS.Domain.Restaurant;

public enum KotLineStatus
{
    Pending = 0,
    Sent = 1,
    Accepted = 2,
    Preparing = 3,
    Ready = 4,
    Served = 5,
    Cancelled = 6,
}

public class OrderLine
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }

    /// <summary>Tracked per line so a kitchen only sees the items routed to its station, not the whole order.</summary>
    public KotLineStatus KotStatus { get; set; } = KotLineStatus.Pending;
}
