using UniversalPOS.Domain.Inventory;

namespace UniversalPOS.Application.Inventory.Dtos;

public class StockOnHandDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsBelowReorderLevel { get; set; }
}

public class StockLedgerEntryDto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public long ReferenceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateStockAdjustmentLineRequest
{
    public long ProductId { get; set; }
    public decimal QuantityChange { get; set; }
}

public class CreateStockAdjustmentRequest
{
    public StockAdjustmentReason Reason { get; set; }
    public string? Notes { get; set; }
    public List<CreateStockAdjustmentLineRequest> Lines { get; set; } = new();
}

public class StockAdjustmentDto
{
    public long Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<CreateStockAdjustmentLineRequest> Lines { get; set; } = new();
}

public class StockReconciliationFlagDto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long SaleHeaderId { get; set; }
    public string? SaleInvoiceNumber { get; set; }
    public decimal ShortfallQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }
}

public class ResolveStockReconciliationFlagRequest
{
    public string? ResolutionNotes { get; set; }
}
