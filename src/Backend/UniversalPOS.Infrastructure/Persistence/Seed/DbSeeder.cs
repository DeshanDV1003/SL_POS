using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Catalog;
using UniversalPOS.Domain.Identity;
using UniversalPOS.Domain.Organization;
using UniversalPOS.Domain.Restaurant;

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
                Domain.Identity.PermissionCodes.TaxRateManage, Domain.Identity.PermissionCodes.SupplierManage,
                Domain.Identity.PermissionCodes.CustomerManage, Domain.Identity.PermissionCodes.StockAdjustmentApprove,
                Domain.Identity.PermissionCodes.GoodsReceiptCreate, Domain.Identity.PermissionCodes.OrderCreate,
                Domain.Identity.PermissionCodes.OrderBill, Domain.Identity.PermissionCodes.KdsUpdate,
                Domain.Identity.PermissionCodes.CashShiftOpen, Domain.Identity.PermissionCodes.DayEndReportFinalize,
                Domain.Identity.PermissionCodes.LoyaltyAdjust, Domain.Identity.PermissionCodes.PromotionManage,
                Domain.Identity.PermissionCodes.PurchaseInvoiceCreate, Domain.Identity.PermissionCodes.SupplierPaymentCreate,
                Domain.Identity.PermissionCodes.StockReconciliationResolve,
            },
            ["Cashier"] = new[]
            {
                Domain.Identity.PermissionCodes.SalesCreate, Domain.Identity.PermissionCodes.SalesReprint,
                Domain.Identity.PermissionCodes.CashMovementCreate, Domain.Identity.PermissionCodes.OrderCreate,
                Domain.Identity.PermissionCodes.OrderBill, Domain.Identity.PermissionCodes.CashShiftOpen,
            },
            ["KitchenStaff"] = new[]
            {
                Domain.Identity.PermissionCodes.TableManage, Domain.Identity.PermissionCodes.KdsUpdate,
            },
        };

        var result = new Dictionary<string, Role>();
        foreach (var (roleName, permissionCodes) in roleDefinitions)
        {
            var role = await db.Roles.Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.CompanyId == null && r.Name == roleName);

            if (role is null)
            {
                role = new Role { Name = roleName, IsSystemRole = true, CompanyId = null };
                foreach (var code in permissionCodes)
                {
                    role.RolePermissions.Add(new RolePermission { Permission = Perm(code) });
                }
                db.Roles.Add(role);
            }
            else
            {
                // A system role that already exists in the database from an earlier
                // deployment must still pick up permission codes added to its
                // definition since then — otherwise a newly introduced permission
                // (like inventory.reconciliation.resolve) never reaches Manager/Admin
                // on an existing database, only on a brand-new one.
                var existingCodes = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
                foreach (var code in permissionCodes)
                {
                    var permission = Perm(code);
                    if (existingCodes.Add(permission.Id))
                    {
                        role.RolePermissions.Add(new RolePermission { Permission = permission });
                    }
                }
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

        var vat = new TaxRate { CompanyId = company.Id, Name = "VAT 18%", Type = TaxType.Vat, Percentage = 18m, IsInclusive = true, EffectiveFromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAtUtc = DateTime.UtcNow };
        db.TaxRates.Add(vat);

        var portion = new Domain.Catalog.Unit { CompanyId = company.Id, Name = "Portion", Abbreviation = "pc", ConversionFactor = 1m, CreatedAtUtc = DateTime.UtcNow };
        db.Units.Add(portion);
        await db.SaveChangesAsync();

        var riceAndCurry = new Category { CompanyId = company.Id, Name = "Rice & Curry", DefaultTaxRateId = vat.Id, CreatedAtUtc = DateTime.UtcNow };
        var shortEats = new Category { CompanyId = company.Id, Name = "Short Eats & Kottu", DefaultTaxRateId = vat.Id, CreatedAtUtc = DateTime.UtcNow };
        var beverages = new Category { CompanyId = company.Id, Name = "Beverages", DefaultTaxRateId = vat.Id, CreatedAtUtc = DateTime.UtcNow };
        db.Categories.AddRange(riceAndCurry, shortEats, beverages);
        await db.SaveChangesAsync();

        var mainKitchen = new KitchenStation { CompanyId = company.Id, BranchId = colomboBranch.Id, Name = "Main Kitchen", Category = StationCategory.Kitchen };
        var bar = new KitchenStation { CompanyId = company.Id, BranchId = colomboBranch.Id, Name = "Bar", Category = StationCategory.Bar };
        db.KitchenStations.AddRange(mainKitchen, bar);
        await db.SaveChangesAsync();

        db.Products.AddRange(
            new Product { CompanyId = company.Id, CategoryId = riceAndCurry.Id, UnitId = portion.Id, Sku = "CS-RC-001", Name = "Chicken Rice & Curry", CostPrice = 380m, SellingPrice = 750m, ReorderLevel = 0, MinStock = 0, MaxStock = 0, DefaultKitchenStationId = mainKitchen.Id, CreatedAtUtc = DateTime.UtcNow },
            new Product { CompanyId = company.Id, CategoryId = riceAndCurry.Id, UnitId = portion.Id, Sku = "CS-RC-002", Name = "Vegetable Rice & Curry", CostPrice = 220m, SellingPrice = 500m, ReorderLevel = 0, MinStock = 0, MaxStock = 0, DefaultKitchenStationId = mainKitchen.Id, CreatedAtUtc = DateTime.UtcNow },
            new Product { CompanyId = company.Id, CategoryId = shortEats.Id, UnitId = portion.Id, Sku = "CS-SE-001", Name = "Chicken Kottu", CostPrice = 420m, SellingPrice = 850m, ReorderLevel = 0, MinStock = 0, MaxStock = 0, DefaultKitchenStationId = mainKitchen.Id, CreatedAtUtc = DateTime.UtcNow },
            new Product { CompanyId = company.Id, CategoryId = beverages.Id, UnitId = portion.Id, Sku = "CS-BV-001", Name = "King Coconut", CostPrice = 80m, SellingPrice = 200m, ReorderLevel = 0, MinStock = 0, MaxStock = 0, DefaultKitchenStationId = bar.Id, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var groundFloor = new Floor { CompanyId = company.Id, BranchId = colomboBranch.Id, Name = "Ground Floor", SortOrder = 1 };
        db.Floors.Add(groundFloor);
        await db.SaveChangesAsync();

        db.DiningTables.AddRange(
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T1", Capacity = 2 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T2", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T3", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T4", Capacity = 6 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T5", Capacity = 2 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T6", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T7", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T8", Capacity = 6 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T9", Capacity = 2 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T10", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T11", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T12", Capacity = 6 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T13", Capacity = 2 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T14", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T15", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T16", Capacity = 6 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T17", Capacity = 2 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T18", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T19", Capacity = 4 },
            new DiningTable { CompanyId = company.Id, BranchId = colomboBranch.Id, FloorId = groundFloor.Id, Name = "T20", Capacity = 6 });
        await db.SaveChangesAsync();
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
        // Reserved for the account-lockout integration test only — never used for a
        // legitimate login in any other test, so intentionally failing it 5 times
        // never blocks unrelated tests that share this database (see
        // AuthEndpointTests.Login_AfterFiveFailedAttempts_LocksAccount).
        await AddUserAsync(db, hasher, company.Id, "qa.lockouttest", "QA Lockout Test Account", roles["Cashier"], nugegodaBranch.Id);

        var vat = new TaxRate { CompanyId = company.Id, Name = "VAT 18%", Type = TaxType.Vat, Percentage = 18m, IsInclusive = true, EffectiveFromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAtUtc = DateTime.UtcNow };
        var vatExempt = new TaxRate { CompanyId = company.Id, Name = "VAT Exempt (essential food)", Type = TaxType.Vat, Percentage = 0m, IsInclusive = true, EffectiveFromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAtUtc = DateTime.UtcNow };
        db.TaxRates.AddRange(vat, vatExempt);

        var each = new Domain.Catalog.Unit { CompanyId = company.Id, Name = "Each", Abbreviation = "ea", ConversionFactor = 1m, CreatedAtUtc = DateTime.UtcNow };
        var kilogram = new Domain.Catalog.Unit { CompanyId = company.Id, Name = "Kilogram", Abbreviation = "kg", ConversionFactor = 1m, CreatedAtUtc = DateTime.UtcNow };
        db.Units.AddRange(each, kilogram);
        await db.SaveChangesAsync();

        var groceries = new Category { CompanyId = company.Id, Name = "Groceries", DefaultTaxRateId = vatExempt.Id, CreatedAtUtc = DateTime.UtcNow };
        var dairy = new Category { CompanyId = company.Id, Name = "Dairy", DefaultTaxRateId = vat.Id, CreatedAtUtc = DateTime.UtcNow };
        var beverages = new Category { CompanyId = company.Id, Name = "Beverages", DefaultTaxRateId = vat.Id, CreatedAtUtc = DateTime.UtcNow };
        db.Categories.AddRange(groceries, dairy, beverages);
        await db.SaveChangesAsync();

        var rice = new Product { CompanyId = company.Id, CategoryId = groceries.Id, UnitId = kilogram.Id, Sku = "LFM-GR-001", Name = "Basmathi Rice 5kg", CostPrice = 1450m, SellingPrice = 1690m, ReorderLevel = 20, MinStock = 10, MaxStock = 200, CreatedAtUtc = DateTime.UtcNow };
        rice.Barcodes.Add(new ProductBarcode { Barcode = "4791234500019", IsPrimary = true });

        var milk = new Product { CompanyId = company.Id, CategoryId = dairy.Id, UnitId = each.Id, Sku = "LFM-DY-001", Name = "Full Cream Milk Powder 400g", CostPrice = 780m, SellingPrice = 895m, ReorderLevel = 30, MinStock = 15, MaxStock = 300, TrackExpiry = true, CreatedAtUtc = DateTime.UtcNow };
        milk.Barcodes.Add(new ProductBarcode { Barcode = "4791234500026", IsPrimary = true });

        var cola = new Product { CompanyId = company.Id, CategoryId = beverages.Id, UnitId = each.Id, Sku = "LFM-BV-001", Name = "Cola 1.5L Bottle", CostPrice = 210m, SellingPrice = 280m, ReorderLevel = 40, MinStock = 20, MaxStock = 400, CreatedAtUtc = DateTime.UtcNow };
        cola.Barcodes.Add(new ProductBarcode { Barcode = "4791234500033", IsPrimary = true });

        db.Products.AddRange(rice, milk, cola);
        await db.SaveChangesAsync();
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
