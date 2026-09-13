namespace UniversalPOS.Application.Catalog.Dtos;

public class CategoryDto
{
    public long Id { get; set; }
    public long? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? DefaultTaxRateId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCategoryRequest
{
    public long? ParentCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? DefaultTaxRateId { get; set; }
}

public class BrandDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateBrandRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UnitDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public long? BaseUnitId { get; set; }
    public decimal ConversionFactor { get; set; }
}

public class CreateUnitRequest
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public long? BaseUnitId { get; set; }
    public decimal ConversionFactor { get; set; } = 1m;
}

public class TaxRateDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public bool IsInclusive { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public bool IsActive { get; set; }
}

public class CreateTaxRateRequest
{
    public string Name { get; set; } = string.Empty;
    public Domain.Catalog.TaxType Type { get; set; } = Domain.Catalog.TaxType.Vat;
    public decimal Percentage { get; set; }
    public bool IsInclusive { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
}

public class ProductSummaryDto
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public long? CategoryId { get; set; }
    public long? BrandId { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<string> Barcodes { get; set; } = Array.Empty<string>();
}

public class CreateProductRequest
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? CategoryId { get; set; }
    public long? BrandId { get; set; }
    public long UnitId { get; set; }
    public long? TaxRateId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal? WholesalePrice { get; set; }
    public decimal? MinSellingPrice { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal MinStock { get; set; }
    public decimal MaxStock { get; set; }
    public bool TrackBatches { get; set; }
    public bool TrackExpiry { get; set; }
    public bool TrackSerial { get; set; }
    public bool IsWeighted { get; set; }
    public IReadOnlyList<string> Barcodes { get; set; } = Array.Empty<string>();
}
