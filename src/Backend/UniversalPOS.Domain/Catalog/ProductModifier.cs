namespace UniversalPOS.Domain.Catalog;

/// <summary>e.g. "Extra cheese" (+150.00) within a ProductModifierGroup.</summary>
public class ProductModifier
{
    public long Id { get; set; }
    public long ModifierGroupId { get; set; }
    public ProductModifierGroup ModifierGroup { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public decimal PriceAdjustment { get; set; }
    public bool IsActive { get; set; } = true;
}
