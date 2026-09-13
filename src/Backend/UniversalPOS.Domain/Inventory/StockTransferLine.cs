namespace UniversalPOS.Domain.Inventory;

public class StockTransferLine
{
    public long Id { get; set; }
    public long StockTransferId { get; set; }
    public StockTransfer StockTransfer { get; set; } = null!;

    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
}
