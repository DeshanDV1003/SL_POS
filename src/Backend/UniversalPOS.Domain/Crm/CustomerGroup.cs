using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Crm;

public class CustomerGroup : AuditableEntity
{
    public long CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Default discount applied to members of this group, e.g. a wholesale tier. 0 for none.</summary>
    public decimal DefaultDiscountPercentage { get; set; }

    public bool IsActive { get; set; } = true;
}
