namespace UniversalPOS.Domain.Sales;

public class SaleLine
{
    public long Id { get; set; }
    public long SaleHeaderId { get; set; }
    public SaleHeader SaleHeader { get; set; } = null!;

    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal TaxRatePercentage { get; set; }

    public decimal LineDiscountAmount { get; set; }
    public decimal LineTaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}
