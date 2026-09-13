using UniversalPOS.Domain.Common;
using UniversalPOS.Domain.Organization;

namespace UniversalPOS.Domain.Identity;

public class AppUser : AuditableEntity
{
    public long CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string FullName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Hashed quick-login PIN for POS terminal use, independent of the password.</summary>
    public string? PinHash { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsLockedOut { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedOutUntilUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public ICollection<UserBranch> UserBranches { get; set; } = new List<UserBranch>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
