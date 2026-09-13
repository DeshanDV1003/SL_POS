using Microsoft.EntityFrameworkCore;
using UniversalPOS.Domain.Auditing;
using UniversalPOS.Domain.Fiscal;
using UniversalPOS.Domain.Identity;
using UniversalPOS.Domain.Organization;

namespace UniversalPOS.Application.Common.Interfaces;

/// <summary>
/// Exposes the persistence surface Application-layer services depend on, without the
/// Application layer referencing EF Core or Infrastructure directly.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Terminal> Terminals { get; }

    DbSet<AppUser> Users { get; }
    DbSet<UserBranch> UserBranches { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }

    DbSet<FiscalTransmission> FiscalTransmissions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
