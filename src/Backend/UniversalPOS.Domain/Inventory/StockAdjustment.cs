namespace UniversalPOS.Domain.Inventory;

public enum StockAdjustmentReason
{
    Wastage = 0,
    Damage = 1,
    Expiry = 2,
    CountDiscrepancy = 3,
    Other = 4,
}

public enum StockAdjustmentStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>
/// A manual stock correction. Requires inventory.adjust to create and, above the
/// configured threshold behavior described in docs/architecture.md, a second
/// authorized user to approve before it posts StockLedger rows — never applied
/// silently.
/// </summary>
public class StockAdjustment
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }

    public StockAdjustmentReason Reason { get; set; }
    public StockAdjustmentStatus Status { get; set; } = StockAdjustmentStatus.Pending;
    public string? Notes { get; set; }

    public long RequestedByUserId { get; set; }
    public long? ApprovedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public ICollection<StockAdjustmentLine> Lines { get; set; } = new List<StockAdjustmentLine>();
}
