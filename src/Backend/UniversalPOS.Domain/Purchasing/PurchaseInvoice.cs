namespace UniversalPOS.Domain.Purchasing;

public enum PurchaseInvoiceStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
}

/// <summary>The supplier's own bill for goods received. AmountPaid is a derived summary, updated only alongside a SupplierPayment row.</summary>
public class PurchaseInvoice
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long SupplierId { get; set; }
    public long? GoodsReceivedNoteId { get; set; }

    /// <summary>The supplier's own invoice reference — free text, not a sequence we control.</summary>
    public string SupplierInvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }

    public PurchaseInvoiceStatus Status { get; set; } = PurchaseInvoiceStatus.Unpaid;
    public DateTime CreatedAtUtc { get; set; }
}
