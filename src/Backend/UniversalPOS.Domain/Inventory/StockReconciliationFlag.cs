namespace UniversalPOS.Domain.Inventory;

public enum StockReconciliationFlagStatus
{
    Open = 0,
    Resolved = 1,
}

/// <summary>
/// Raised when an offline-synced sale (see docs/architecture.md §10) drives a
/// product's stock below zero — the sale still posts (a completed cash sale a
/// customer already paid for and left with can't be refused after the fact just
/// because the terminal was offline when it happened), but a manager needs a real,
/// queued item to review and reconcile with a physical count, not a silently
/// negative StockOnHand row nobody notices.
/// </summary>
public class StockReconciliationFlag
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long ProductId { get; set; }

    public long SaleHeaderId { get; set; }

    /// <summary>How far below zero QuantityOnHand went as a result of this sale (positive number).</summary>
    public decimal ShortfallQuantity { get; set; }

    public StockReconciliationFlagStatus Status { get; set; } = StockReconciliationFlagStatus.Open;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public long? ResolvedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }
}
