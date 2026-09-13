namespace UniversalPOS.Domain.Inventory;

public class StockCountLine
{
    public long Id { get; set; }
    public long StockCountId { get; set; }
    public StockCount StockCount { get; set; } = null!;

    public long ProductId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal? CountedQuantity { get; set; }
}
