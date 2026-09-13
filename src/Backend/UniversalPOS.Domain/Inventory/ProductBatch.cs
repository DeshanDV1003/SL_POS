namespace UniversalPOS.Domain.Inventory;

/// <summary>A received lot of a TrackBatches/TrackExpiry Product. StockLedger rows may reference a specific batch.</summary>
public class ProductBatch
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long ProductId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public DateTime ReceivedDate { get; set; }

    public decimal QuantityReceived { get; set; }
    public decimal QuantityRemaining { get; set; }
}
