namespace UniversalPOS.Domain.Purchasing;

/// <summary>
/// Records goods actually received from a Supplier. Posting a GRN is the single point
/// where purchasing meets inventory: it creates StockLedger rows (and ProductBatch rows
/// for batch/expiry-tracked products) in the same transaction as the GRN itself, and
/// updates the originating PurchaseOrder's QuantityReceived. A GRN is immutable once
/// posted — a correction is a new document (a purchase return), never an edit.
/// </summary>
public class GoodsReceivedNote
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long SupplierId { get; set; }
    public long? PurchaseOrderId { get; set; }

    public string GrnNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public long ReceivedByUserId { get; set; }

    public ICollection<GoodsReceivedNoteLine> Lines { get; set; } = new List<GoodsReceivedNoteLine>();
}
