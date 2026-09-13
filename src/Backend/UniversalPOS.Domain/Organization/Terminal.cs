using UniversalPOS.Domain.Common;

namespace UniversalPOS.Domain.Organization;

public class Terminal : AuditableEntity
{
    public long BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>Unique per Branch.</summary>
    public string Code { get; set; } = string.Empty;

    public string? DeviceIdentifier { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSeenAtUtc { get; set; }
}
