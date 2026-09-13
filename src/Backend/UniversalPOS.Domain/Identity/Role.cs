namespace UniversalPOS.Domain.Identity;

public class Role
{
    public long Id { get; set; }

    /// <summary>Null for system-seeded global roles (Admin, Manager, Cashier, KitchenStaff); set for a Company's custom role.</summary>
    public long? CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
