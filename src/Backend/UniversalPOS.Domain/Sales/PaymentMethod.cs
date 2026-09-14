namespace UniversalPOS.Domain.Sales;

public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    BankTransfer = 2,
    Digital = 3,
    Credit = 4,

    /// <summary>Redeems the sale's customer's loyalty point balance instead of an external tender — handled entirely inside SalesService, never routed through IPaymentProvider.</summary>
    LoyaltyPoints = 5,
}
