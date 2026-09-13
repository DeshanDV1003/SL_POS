namespace UniversalPOS.Application.Inventory.Dtos;

public class CreateStockTransferLineRequest
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class CreateStockTransferRequest
{
    public long ToBranchId { get; set; }
    public List<CreateStockTransferLineRequest> Lines { get; set; } = new();
}

public class StockTransferDto
{
    public long Id { get; set; }
    public long FromBranchId { get; set; }
    public long ToBranchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<CreateStockTransferLineRequest> Lines { get; set; } = new();
}
