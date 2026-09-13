using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Purchasing.Dtos;

public class CreatePurchaseInvoiceRequest
{
    public long SupplierId { get; set; }
    public long? GoodsReceivedNoteId { get; set; }
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
}

public class PurchaseInvoiceDto
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RecordSupplierPaymentRequest
{
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? ReferenceNo { get; set; }
}
