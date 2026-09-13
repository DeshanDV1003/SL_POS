using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Catalog;

public class Product : AuditableEntity
{
    public long CompanyId { get; set; }

    public long? CategoryId { get; set; }
    public long? BrandId { get; set; }
    public long UnitId { get; set; }

    /// <summary>Unique per Company. The internal stock-keeping identifier, distinct from any printed barcode.</summary>
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public decimal? MinSellingPrice { get; set; }

    /// <summary>Overrides the Category's DefaultTaxRateId when set.</summary>
    public long? TaxRateId { get; set; }

    public decimal ReorderLevel { get; set; }
    public decimal MinStock { get; set; }
    public decimal MaxStock { get; set; }

    public bool TrackBatches { get; set; }
    public bool TrackExpiry { get; set; }
    public bool TrackSerial { get; set; }
    public bool IsWeighted { get; set; }

    /// <summary>A bundle/recipe: its stock deduction at sale time comes from ProductComponent rows, not its own StockLedger.</summary>
    public bool IsComposite { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ProductBarcode> Barcodes { get; set; } = new List<ProductBarcode>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductComponent> Components { get; set; } = new List<ProductComponent>();
    public ICollection<ProductModifierGroup> ModifierGroups { get; set; } = new List<ProductModifierGroup>();
}
