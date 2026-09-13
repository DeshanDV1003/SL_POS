using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Identity;
using UniversalPOS.Domain.Organization;

namespace UniversalPOS.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds realistic development/demo data: two sample companies (a restaurant chain and
/// a supermarket) covering both major business types, the full permission catalog, and
/// the default system roles. Real deployments replace this with their own data — this
/// exists for development, demos, and the E2E test scenarios in docs/architecture.md.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IPasswordHasher passwordHasher)
    {
        await SeedPermissionsAsync(db);
        var systemRoles = await SeedSystemRolesAsync(db);

        if (!await db.Companies.AnyAsync())
        {
            await SeedRestaurantCompanyAsync(db, passwordHasher, systemRoles);
            await SeedSupermarketCompanyAsync(db, passwordHasher, systemRoles);
        }
    }

    private static async Task SeedPermissionsAsync(ApplicationDbContext db)
    {
        var existingCodes = await db.Permissions.Select(p => p.Code).ToListAsync();
        var missing = Domain.Identity.PermissionCodes.All
            .Where(p => !existingCodes.Contains(p.Code))
            .Select(p => new Permission { Code = p.Code, Category = p.Category, Description = p.Description });

        db.Permissions.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static async Task<Dictionary<string, Role>> SeedSystemRolesAsync(ApplicationDbContext db)
    {
        var allPermissions = await db.Permissions.ToListAsync();
        Permission Perm(string code) => allPermissions.Single(p => p.Code == code);

        var roleDefinitions = new Dictionary<string, string[]>
        {
            ["Admin"] = allPermissions.Select(p => p.Code).ToArray(),
            ["Manager"] = new[]
            {
                Domain.Identity.PermissionCodes.SalesCreate, Domain.Identity.PermissionCodes.SalesVoid,
                Domain.Identity.PermissionCodes.SalesRefund, Domain.Identity.PermissionCodes.SalesDiscountApply,
                Domain.Identity.PermissionCodes.SalesPriceOverride, Domain.Identity.PermissionCodes.SalesReprint,
                Domain.Identity.PermissionCodes.CashDrawerOpen, Domain.Identity.PermissionCodes.CashShiftClose,
                Domain.Identity.PermissionCodes.CashMovementCreate, Domain.Identity.PermissionCodes.InventoryAdjust,
                Domain.Identity.PermissionCodes.InventoryTransfer, Domain.Identity.PermissionCodes.InventoryCount,
                Domain.Identity.PermissionCodes.ProductManage, Domain.Identity.PermissionCodes.PurchaseOrderCreate,
                Domain.Identity.PermissionCodes.PurchaseOrderApprove, Domain.Identity.PermissionCodes.ReportsViewSales,
                Domain.Identity.PermissionCodes.ReportsViewFinancial, Domain.Identity.PermissionCodes.TableManage,
                Domain.Identity.PermissionCodes.KotCancel, Domain.Identity.PermissionCodes.AuditView,
            },
            ["Cashier"] = new[]
            {
                Domain.Identity.PermissionCodes.SalesCreate, Domain.Identity.PermissionCodes.SalesReprint,
                Domain.Identity.PermissionCodes.CashMovementCreate,
            },
            ["KitchenStaff"] = new[]
            {
                Domain.Identity.PermissionCodes.TableManage,
            },
        };

        var result = new Dictionary<string, Role>();
        foreach (var (roleName, permissionCodes) in roleDefinitions)
        {
            var role = await db.Roles.Include(r => r.RolePermissions)
                .SingleOrDefaultAsync(r => r.CompanyId == null && r.Name == roleName);

            if (role is null)
            {
                role = new Role { Name = roleName, IsSystemRole = true, CompanyId = null };
                foreach (var code in permissionCodes)
                {
                    role.RolePermissions.Add(new RolePermission { Permission = Perm(code) });
                }
                db.Roles.Add(role);
            }

            result[roleName] = role;
        }

        await db.SaveChangesAsync();
        return result;
    }

    private static async Task SeedRestaurantCompanyAsync(ApplicationDbContext db, IPasswordHasher hasher, Dictionary<string, Role> roles)
    {
        var company = new Company
        {
            Name = "Ceylon Spice Restaurant (Pvt) Ltd",
            LegalName = "Ceylon Spice Restaurant (Private) Limited",
            TaxRegistrationNo = "134567890",
            IsVatRegistered = true,
            VatRegisteredFromDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DefaultCurrencyCode = "LKR",
            CreatedAtUtc = DateTime.UtcNow,
        };

        var colomboBranch = new Branch
        {
            Company = company,
            Name = "Ceylon Spice - Colombo 07",
            Code = "CS-COL07",
            Address = "142 Horton Place",
            City = "Colombo",
            BusinessTypeFlags = BusinessType.Restaurant,
            ServiceChargeRate = 0.10m,
            CreatedAtUtc = DateTime.UtcNow,
        };
        var kandyBranch = new Branch
        {
            Company = company,
            Name = "Ceylon Spice - Kandy",
            Code = "CS-KDY01",
            Address = "45 Dalada Veediya",
            City = "Kandy",
            BusinessTypeFlags = BusinessType.Restaurant | BusinessType.Cafe,
            ServiceChargeRate = 0.10m,
            CreatedAtUtc = DateTime.UtcNow,
        };
        company.Branches.Add(colomboBranch);
        company.Branches.Add(kandyBranch);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        db.Terminals.AddRange(
            new Terminal { BranchId = colomboBranch.Id, Name = "Front Counter 1", Code = "T1", CreatedAtUtc = DateTime.UtcNow },
            new Terminal { BranchId = colomboBranch.Id, Name = "Front Counter 2", Code = "T2", CreatedAtUtc = DateTime.UtcNow },
            new Terminal { BranchId = kandyBranch.Id, Name = "Front Counter 1", Code = "T1", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await AddUserAsync(db, hasher, company.Id, "admin.cs", "Nimal Perera", roles["Admin"], colomboBranch.Id, kandyBranch.Id);
        await AddUserAsync(db, hasher, company.Id, "manager.cs", "Shanika Fernando", roles["Manager"], colomboBranch.Id);
        await AddUserAsync(db, hasher, company.Id, "cashier.cs", "Dilani Wickramasinghe", roles["Cashier"], colomboBranch.Id);
        await AddUserAsync(db, hasher, company.Id, "kitchen.cs", "Sunil Rathnayake", roles["KitchenStaff"], colomboBranch.Id);
    }

    private static async Task SeedSupermarketCompanyAsync(ApplicationDbContext db, IPasswordHasher hasher, Dictionary<string, Role> roles)
    {
        var company = new Company
        {
            Name = "Lanka Fresh Mart (Pvt) Ltd",
            LegalName = "Lanka Fresh Mart (Private) Limited",
            TaxRegistrationNo = "198765432",
            IsVatRegistered = true,
            VatRegisteredFromDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DefaultCurrencyCode = "LKR",
            CreatedAtUtc = DateTime.UtcNow,
        };

        var nugegodaBranch = new Branch
        {
            Company = company,
            Name = "Lanka Fresh Mart - Nugegoda",
            Code = "LFM-NUG01",
            Address = "88 High Level Road",
            City = "Nugegoda",
            BusinessTypeFlags = BusinessType.Supermarket | BusinessType.Grocery,
            CreatedAtUtc = DateTime.UtcNow,
        };
        company.Branches.Add(nugegodaBranch);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        db.Terminals.AddRange(
            new Terminal { BranchId = nugegodaBranch.Id, Name = "Checkout 1", Code = "T1", CreatedAtUtc = DateTime.UtcNow },
            new Terminal { BranchId = nugegodaBranch.Id, Name = "Checkout 2", Code = "T2", CreatedAtUtc = DateTime.UtcNow },
            new Terminal { BranchId = nugegodaBranch.Id, Name = "Checkout 3", Code = "T3", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await AddUserAsync(db, hasher, company.Id, "admin.lfm", "Chaminda Silva", roles["Admin"], nugegodaBranch.Id);
        await AddUserAsync(db, hasher, company.Id, "manager.lfm", "Kumari Jayasuriya", roles["Manager"], nugegodaBranch.Id);
        await AddUserAsync(db, hasher, company.Id, "cashier.lfm", "Ruwan Bandara", roles["Cashier"], nugegodaBranch.Id);
    }

    private static async Task AddUserAsync(
        ApplicationDbContext db,
        IPasswordHasher hasher,
        long companyId,
        string username,
        string fullName,
        Role role,
        params long[] branchIds)
    {
        var user = new AppUser
        {
            CompanyId = companyId,
            Username = username,
            FullName = fullName,
            PasswordHash = hasher.Hash("Passw0rd!"),
            CreatedAtUtc = DateTime.UtcNow,
        };

        foreach (var branchId in branchIds)
        {
            user.UserBranches.Add(new UserBranch { BranchId = branchId });
        }
        user.UserRoles.Add(new UserRole { Role = role });

        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
