namespace UniversalPOS.Domain.Restaurant;

public enum TableSessionStatus
{
    Open = 0,
    Billing = 1,
    Closed = 2,
}

/// <summary>Opens when a table is seated, closes on bill payment. Supports merge/split/transfer by relinking Order<->Table (Phase 7 continuation).</summary>
public class TableSession
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long TableId { get; set; }

    public TableSessionStatus Status { get; set; } = TableSessionStatus.Open;
    public long? WaiterUserId { get; set; }

    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}
