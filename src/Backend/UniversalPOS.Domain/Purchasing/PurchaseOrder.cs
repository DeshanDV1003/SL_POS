namespace UniversalPOS.Domain.Purchasing;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    PartiallyReceived = 3,
    Received = 4,
    Cancelled = 5,
}

public class PurchaseOrder
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    /// <summary>Sequential per Branch, like an invoice number.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public long CreatedByUserId { get; set; }
    public long? ApprovedByUserId { get; set; }

    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string? Notes { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}
