using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Catalog;

/// <summary>e.g. "Kilogram" (base) and "Gram" with BaseUnitId -> Kilogram, ConversionFactor 0.001.</summary>
public class Unit : AuditableEntity
{
    public long CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;

    public long? BaseUnitId { get; set; }

    /// <summary>Multiply a quantity in this unit by this factor to get the equivalent quantity in BaseUnit.</summary>
    public decimal ConversionFactor { get; set; } = 1m;

    public bool IsActive { get; set; } = true;
}
