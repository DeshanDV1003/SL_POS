using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Restaurant;

namespace UniversalPOS.Application.Restaurant.Dtos;

public class TableDto
{
    public long Id { get; set; }
    public long FloorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? OpenOrderId { get; set; }
}

public class FloorDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TableDto> Tables { get; set; } = new();
}

public class OrderLineDto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
    public string KotStatus { get; set; } = string.Empty;
}

public class OrderDto
{
    public long Id { get; set; }
    public long? TableId { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
}

public class AddOrderLineRequest
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public class OpenTableRequest
{
    public OrderType OrderType { get; set; } = OrderType.DineIn;
}

public class UpdateTicketStatusRequest
{
    public TicketStatus Status { get; set; }
}

public class PreparationTicketLineDto
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Notes { get; set; }
}

public class PreparationTicketDto
{
    public long Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public long OrderId { get; set; }
    public long? TableId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public List<PreparationTicketLineDto> Lines { get; set; } = new();
}

public class BillOrderRequest
{
    public long TerminalId { get; set; }
    public List<CreateSalePaymentRequest> Payments { get; set; } = new();
}
