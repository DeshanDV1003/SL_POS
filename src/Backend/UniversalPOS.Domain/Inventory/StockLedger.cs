namespace UniversalPOS.Domain.Inventory;

/// <summary>
/// Immutable, append-only record of every stock movement. Never updated or deleted —
/// a correction is posted as a new offsetting row, same as accounting ledgers. Current
/// stock (StockOnHand) is a derived, transactionally-maintained aggregate over this
/// table, never the sole source of truth.
/// </summary>
public class StockLedger
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long ProductId { get; set; }
    public long? BatchId { get; set; }

    public StockMovementType MovementType { get; set; }

    /// <summary>Signed: positive for stock coming in, negative for stock going out.</summary>
    public decimal QuantityChange { get; set; }

    /// <summary>e.g. "GoodsReceivedNote", "StockAdjustment", "SaleHeader" — the document that caused this movement.</summary>
    public string ReferenceType { get; set; } = string.Empty;
    public long ReferenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public long? CreatedByUserId { get; set; }
}
