namespace UniversalPOS.Domain.Catalog;

/// <summary>e.g. a T-shirt Product with variants for each Size/Color combination.</summary>
public class ProductVariant
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }

    /// <summary>Added to (or subtracted from, if negative) the parent Product's SellingPrice.</summary>
    public decimal PriceAdjustment { get; set; }

    public bool IsActive { get; set; } = true;
}
