namespace UniversalPOS.Domain.Identity;

/// <summary>Seeded, code-defined action-level permissions. See PermissionCodes for the canonical list.</summary>
public class Permission
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
