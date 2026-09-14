namespace UniversalPOS.Domain.Sales;

/// <summary>
/// A customer-entered code, distinct from the automatic rule-matched Promotion engine.
/// Applied as a flat reduction to the sale's GrandTotal at checkout (it does not
/// redistribute across lines or change the tax base — a documented v1 simplification,
/// like a manufacturer coupon rather than a recalculated line discount).
/// </summary>
public class Coupon
{
    public long Id { get; set; }
    public long CompanyId { get; set; }

    /// <summary>Unique per Company, case-insensitive (stored upper-cased).</summary>
    public string Code { get; set; } = string.Empty;

    public PromotionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }

    /// <summary>The sale's SubTotal (before this coupon) must be at least this amount for the coupon to apply.</summary>
    public decimal MinSaleAmount { get; set; }

    /// <summary>Null means unlimited redemptions.</summary>
    public int? MaxRedemptions { get; set; }
    public int TimesRedeemed { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
}
