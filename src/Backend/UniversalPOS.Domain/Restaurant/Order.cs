namespace UniversalPOS.Domain.Restaurant;

public enum OrderType
{
    DineIn = 0,
    Takeaway = 1,
    Delivery = 2,
}

public enum OrderStatus
{
    Open = 0,
    Billed = 1,
    Cancelled = 2,
}

/// <summary>
/// The in-progress restaurant/takeaway order before it becomes a SaleHeader at
/// billing time. Distinct from SaleHeader because an order accumulates items over
/// time (a table adds rounds across an evening) and tracks kitchen preparation state,
/// none of which apply once a sale is finalized.
/// </summary>
public class Order
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long? TableSessionId { get; set; }

    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public OrderStatus Status { get; set; } = OrderStatus.Open;

    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Set once billed, linking to the SaleHeader created at checkout.</summary>
    public long? SaleHeaderId { get; set; }

    // Populated only when OrderType is Takeaway or Delivery.
    public string? ContactPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public decimal? DeliveryFee { get; set; }

    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
