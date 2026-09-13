namespace UniversalPOS.Domain.Purchasing;

/// <summary>Reuses Domain.Sales.PaymentMethod so "how we paid a supplier" and "how a customer paid us" share one vocabulary.</summary>
public class SupplierPayment
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long SupplierId { get; set; }
    public long PurchaseInvoiceId { get; set; }

    public decimal Amount { get; set; }
    public Domain.Sales.PaymentMethod Method { get; set; }
    public string? ReferenceNo { get; set; }

    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
