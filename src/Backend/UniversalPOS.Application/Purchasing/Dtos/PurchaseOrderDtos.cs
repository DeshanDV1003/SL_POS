namespace UniversalPOS.Application.Purchasing.Dtos;

public class CreatePurchaseOrderLineRequest
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class CreatePurchaseOrderRequest
{
    public long SupplierId { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = new();
}

public class PurchaseOrderLineDto
{
    public long ProductId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
}

public class PurchaseOrderDto
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
}

public class ReceiveGoodsLineRequest
{
    public long? PurchaseOrderLineId { get; set; }
    public long ProductId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class ReceiveGoodsRequest
{
    public long SupplierId { get; set; }
    public long? PurchaseOrderId { get; set; }
    public List<ReceiveGoodsLineRequest> Lines { get; set; } = new();
}

public class GoodsReceivedNoteDto
{
    public long Id { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public long SupplierId { get; set; }
    public long? PurchaseOrderId { get; set; }
    public DateTime ReceivedDate { get; set; }
    public List<ReceiveGoodsLineRequest> Lines { get; set; } = new();
}
