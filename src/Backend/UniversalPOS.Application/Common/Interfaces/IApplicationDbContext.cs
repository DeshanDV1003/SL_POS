using Microsoft.EntityFrameworkCore;
using UniversalPOS.Domain.Auditing;
using UniversalPOS.Domain.Catalog;
using UniversalPOS.Domain.Crm;
using UniversalPOS.Domain.Fiscal;
using UniversalPOS.Domain.Identity;
using UniversalPOS.Domain.Organization;
using UniversalPOS.Domain.Purchasing;

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

    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Domain.Catalog.Unit> Units { get; }
    DbSet<TaxRate> TaxRates { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductBarcode> ProductBarcodes { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductComponent> ProductComponents { get; }
    DbSet<ProductModifierGroup> ProductModifierGroups { get; }
    DbSet<ProductModifier> ProductModifiers { get; }

    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierProduct> SupplierProducts { get; }

    DbSet<CustomerGroup> CustomerGroups { get; }
    DbSet<Customer> Customers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
