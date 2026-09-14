using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales.Dtos;

public class CreateSaleLineRequest
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal DiscountPercentage { get; set; }

    /// <summary>Requires sales.price.override; also cannot go below Product.MinSellingPrice regardless of permission.</summary>
    public decimal? UnitPriceOverride { get; set; }
}

public class CreateSalePaymentRequest
{
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? InstrumentToken { get; set; }
}

public class CreateSaleRequest
{
    public long TerminalId { get; set; }
    public long? CustomerId { get; set; }
    public InvoiceMode InvoiceMode { get; set; } = InvoiceMode.SimplifiedReceipt;
    public string? PurchaserTin { get; set; }
    public string? PurchaserName { get; set; }
    public string? PurchaserAddress { get; set; }
    public string? ClientIdempotencyKey { get; set; }

    /// <summary>Optional customer-entered coupon code, applied as a flat reduction to GrandTotal. See Domain.Sales.Coupon.</summary>
    public string? CouponCode { get; set; }
    public List<CreateSaleLineRequest> Lines { get; set; } = new();
    public List<CreateSalePaymentRequest> Payments { get; set; } = new();
}

public class SaleLineDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal LineDiscountAmount { get; set; }
    public decimal LineTaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SalePaymentDto
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ProviderReference { get; set; }
}

public class SaleReceiptDto
{
    public long Id { get; set; }
    public string? InvoiceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ServiceChargeTotal { get; set; }
    public decimal CouponDiscountAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal ChangeDue { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public List<SaleLineDto> Lines { get; set; } = new();
    public List<SalePaymentDto> Payments { get; set; } = new();
}

public class CreateHeldBillRequest
{
    public long TerminalId { get; set; }
    public long? CustomerId { get; set; }
    public string? Notes { get; set; }
    public List<CreateSaleLineRequest> Lines { get; set; } = new();
}

public class HeldBillDto
{
    public long Id { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<CreateSaleLineRequest> Lines { get; set; } = new();
}

public class VoidSaleRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class RefundLineRequest
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class RefundSaleRequest
{
    public string Reason { get; set; } = string.Empty;
    public List<RefundLineRequest> Lines { get; set; } = new();

    /// <summary>How the refund is being paid out (e.g. Cash). No provider authorization is performed — this records the outflow, it doesn't "charge" anything.</summary>
    public List<CreateSalePaymentRequest> Payments { get; set; } = new();
}
