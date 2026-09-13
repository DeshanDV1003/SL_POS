namespace UniversalPOS.Domain.Purchasing;

public class GoodsReceivedNoteLine
{
    public long Id { get; set; }
    public long GoodsReceivedNoteId { get; set; }
    public GoodsReceivedNote GoodsReceivedNote { get; set; } = null!;

    public long? PurchaseOrderLineId { get; set; }
    public long ProductId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }

    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
