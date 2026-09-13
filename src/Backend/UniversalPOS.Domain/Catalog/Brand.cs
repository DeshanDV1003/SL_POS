using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Catalog;

public class Brand : AuditableEntity
{
    public long CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
