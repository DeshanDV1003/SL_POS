namespace UniversalPOS.Domain.Sales;

public enum SaleStatus
{
    Held = 0,
    Completed = 1,
    Voided = 2,
    Refunded = 3,
}

public enum InvoiceMode
{
    SimplifiedReceipt = 0,

    /// <summary>Mandated full VAT tax invoice format effective 1 Jul 2026 — captures purchaser TIN/name/address. See docs/research-sri-lanka-pos.md.</summary>
    FullTaxInvoice = 1,
}

/// <summary>
/// Immutable once Status leaves Held. A correction (void/refund) is a new linked
/// record, never an edit to this row — see docs/database-design.md §4.
/// </summary>
public class SaleHeader
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long TerminalId { get; set; }
    public long CashierUserId { get; set; }
    public long? CustomerId { get; set; }

    /// <summary>Sequential and gapless per Branch — assigned only when the sale completes, never on hold.</summary>
    public string? InvoiceNumber { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Held;
    public InvoiceMode InvoiceMode { get; set; } = InvoiceMode.SimplifiedReceipt;
    public string? PurchaserTin { get; set; }
    public string? PurchaserName { get; set; }
    public string? PurchaserAddress { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ServiceChargeTotal { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Deduplicates a retried/offline-synced submission of the same client-originated sale.</summary>
    public string? ClientIdempotencyKey { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Set when Status = Voided, referencing the original completed sale (self-referencing only in the sense of "this is the void of that").</summary>
    public long? VoidedSaleHeaderId { get; set; }
    public string? VoidReason { get; set; }

    public ICollection<SaleLine> Lines { get; set; } = new List<SaleLine>();
    public ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();
}
