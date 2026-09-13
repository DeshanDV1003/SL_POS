namespace UniversalPOS.Domain.Sales;

public class SalePayment
{
    public long Id { get; set; }
    public long SaleHeaderId { get; set; }
    public SaleHeader SaleHeader { get; set; } = null!;

    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Set by the payment provider for non-cash methods; never marked complete without one. See IPaymentProvider.</summary>
    public string? ProviderReference { get; set; }
    public string? ProviderStatus { get; set; }
}
