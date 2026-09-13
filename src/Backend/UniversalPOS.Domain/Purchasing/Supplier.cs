using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Purchasing;

public class Supplier : AuditableEntity
{
    public long CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    /// <summary>Supplier's own TIN, useful for purchase-invoice recordkeeping.</summary>
    public string? TaxRegistrationNo { get; set; }

    public bool IsActive { get; set; } = true;
}
