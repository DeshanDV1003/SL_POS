namespace UniversalPOS.Domain.Sales;

public class HeldBillLine
{
    public long Id { get; set; }
    public long HeldBillId { get; set; }
    public HeldBill HeldBill { get; set; } = null!;

    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal DiscountPercentage { get; set; }
}
