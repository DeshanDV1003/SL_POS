using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Catalog;

public enum TaxType
{
    /// <summary>Standard VAT (18% as of 2024 — see docs/research-sri-lanka-pos.md). Never hardcoded; this is the seeded default only.</summary>
    Vat = 0,

    /// <summary>Special Commodity Levy — a distinct levy from VAT on certain imported essentials, not folded into VAT.</summary>
    SpecialCommodityLevy = 1,

    Other = 2,
}

/// <summary>
/// Admin-configurable tax component. Historical rate changes are preserved (a new row
/// with a later EffectiveFromUtc, not an edit to the old one) so past invoices keep
/// reporting the rate that applied when they were issued.
/// </summary>
public class TaxRate : AuditableEntity
{
    public long CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TaxType Type { get; set; } = TaxType.Vat;
    public decimal Percentage { get; set; }

    /// <summary>If true, product prices already include this tax; if false, it's added at checkout.</summary>
    public bool IsInclusive { get; set; }

    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
