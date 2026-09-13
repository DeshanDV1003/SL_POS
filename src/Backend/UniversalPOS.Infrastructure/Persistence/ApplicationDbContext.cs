using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Auditing;
using UniversalPOS.Domain.Fiscal;
using UniversalPOS.Domain.Identity;
using UniversalPOS.Domain.Organization;

namespace UniversalPOS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Terminal> Terminals => Set<Terminal>();

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    public DbSet<FiscalTransmission> FiscalTransmissions => Set<FiscalTransmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
