namespace UniversalPOS.Domain.Purchasing;

public class PurchaseOrderLine
{
    public long Id { get; set; }
    public long PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public long ProductId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
}
