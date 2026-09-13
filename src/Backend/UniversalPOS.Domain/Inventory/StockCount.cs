namespace UniversalPOS.Domain.Inventory;

public enum StockCountStatus
{
    InProgress = 0,
    Completed = 1,
}

/// <summary>
/// A physical stock count session. SystemQuantity is snapshotted per line when the
/// count is opened; completing the count posts a StockLedger correction (movement
/// type StockCount) for every line where CountedQuantity differs from
/// SystemQuantity — a discrepancy is never silently absorbed into StockOnHand.
/// </summary>
public class StockCount
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }

    public StockCountStatus Status { get; set; } = StockCountStatus.InProgress;
    public long CreatedByUserId { get; set; }
    public long? CompletedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<StockCountLine> Lines { get; set; } = new List<StockCountLine>();
}
