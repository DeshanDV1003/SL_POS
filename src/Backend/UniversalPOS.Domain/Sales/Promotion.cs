namespace UniversalPOS.Domain.Sales;

public enum PromotionDiscountType
{
    Percentage = 0,
    FixedAmount = 1,
}

public enum PromotionScope
{
    AllProducts = 0,
    Category = 1,
    Product = 2,
}

/// <summary>
/// A rule-based, automatic line discount — distinct from the manual per-line discount
/// percentage a cashier can apply. At checkout, the best-matching active promotion for
/// a line and the cashier's manual discount are compared and only the larger one
/// applies (never stacked), per the "priority ordering for conflict resolution" plan
/// in docs/architecture.md.
/// </summary>
public class Promotion
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;

    public PromotionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }

    public PromotionScope Scope { get; set; } = PromotionScope.AllProducts;
    public long? CategoryId { get; set; }
    public long? ProductId { get; set; }

    /// <summary>A line must order at least this many units of the matched product to qualify.</summary>
    public decimal MinQuantity { get; set; } = 1;

    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Higher priority wins when more than one promotion matches the same line.</summary>
    public int Priority { get; set; }
}
