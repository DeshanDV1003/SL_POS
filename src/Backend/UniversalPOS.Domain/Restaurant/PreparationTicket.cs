namespace UniversalPOS.Domain.Restaurant;

public enum TicketStatus
{
    Pending = 0,
    Sent = 1,
    Accepted = 2,
    Preparing = 3,
    Ready = 4,
    Served = 5,
    Cancelled = 6,
}

/// <summary>
/// A KOT (kitchen station) and a BOT (bar station) are the same underlying record
/// type, distinguished only by the KitchenStation's Category — "same architecture,
/// different station" per docs/database-design.md §5, rather than duplicated tables.
/// </summary>
public class PreparationTicket
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long OrderId { get; set; }
    public long KitchenStationId { get; set; }

    /// <summary>Sequential per Branch per business day.</summary>
    public string TicketNumber { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.Pending;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadyAtUtc { get; set; }

    public ICollection<PreparationTicketLine> Lines { get; set; } = new List<PreparationTicketLine>();
}
