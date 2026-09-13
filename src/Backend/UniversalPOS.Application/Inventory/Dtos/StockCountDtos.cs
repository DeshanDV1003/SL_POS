namespace UniversalPOS.Application.Inventory.Dtos;

public class CreateStockCountRequest
{
    /// <summary>Products to include; if empty, every product with a StockOnHand row in the branch is included.</summary>
    public List<long> ProductIds { get; set; } = new();
}

public class SubmitStockCountLineRequest
{
    public long ProductId { get; set; }
    public decimal CountedQuantity { get; set; }
}

public class StockCountLineDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal SystemQuantity { get; set; }
    public decimal? CountedQuantity { get; set; }
}

public class StockCountDto
{
    public long Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<StockCountLineDto> Lines { get; set; } = new();
}
