namespace UniversalPOS.Domain.Inventory;

public class StockAdjustmentLine
{
    public long Id { get; set; }
    public long StockAdjustmentId { get; set; }
    public StockAdjustment StockAdjustment { get; set; } = null!;

    public long ProductId { get; set; }

    /// <summary>Signed: negative for a wastage/damage write-off, positive for a found-stock correction.</summary>
    public decimal QuantityChange { get; set; }
}
