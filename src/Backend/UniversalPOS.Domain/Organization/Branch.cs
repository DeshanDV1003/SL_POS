using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Organization;

public class Branch : AuditableEntity
{
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Unique per Company. Used as the invoice/KOT numbering series key.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }

    public BusinessType BusinessTypeFlags { get; set; } = BusinessType.None;

    /// <summary>Service charge rate applied at billing, e.g. 0.10m for 10%. No SL statute mandates this; purely configurable.</summary>
    public decimal ServiceChargeRate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Terminal> Terminals { get; set; } = new List<Terminal>();
}
