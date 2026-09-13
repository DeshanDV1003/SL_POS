namespace UniversalPOS.Domain.Catalog;

/// <summary>e.g. "Spice Level" or "Add-ons" on a restaurant menu item.</summary>
public class ProductModifierGroup
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;

    public ICollection<ProductModifier> Modifiers { get; set; } = new List<ProductModifier>();
}
