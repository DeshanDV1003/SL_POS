using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Auditing;
using UniversalPOS.Domain.Catalog;
using UniversalPOS.Domain.Crm;
using UniversalPOS.Domain.Fiscal;
using UniversalPOS.Domain.Identity;
using UniversalPOS.Domain.Cash;
using UniversalPOS.Domain.Inventory;
using UniversalPOS.Domain.Organization;
using UniversalPOS.Domain.Purchasing;
using UniversalPOS.Domain.Restaurant;
using UniversalPOS.Domain.Sales;

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

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Domain.Catalog.Unit> Units => Set<Domain.Catalog.Unit>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBarcode> ProductBarcodes => Set<ProductBarcode>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductComponent> ProductComponents => Set<ProductComponent>();
    public DbSet<ProductModifierGroup> ProductModifierGroups => Set<ProductModifierGroup>();
    public DbSet<ProductModifier> ProductModifiers => Set<ProductModifier>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();

    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockOnHand> StockOnHands => Set<StockOnHand>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockAdjustmentLine> StockAdjustmentLines => Set<StockAdjustmentLine>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceivedNote> GoodsReceivedNotes => Set<GoodsReceivedNote>();
    public DbSet<GoodsReceivedNoteLine> GoodsReceivedNoteLines => Set<GoodsReceivedNoteLine>();

    public DbSet<SaleHeader> SaleHeaders => Set<SaleHeader>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<HeldBill> HeldBills => Set<HeldBill>();
    public DbSet<HeldBillLine> HeldBillLines => Set<HeldBillLine>();

    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<TableSession> TableSessions => Set<TableSession>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<KitchenStation> KitchenStations => Set<KitchenStation>();
    public DbSet<PreparationTicket> PreparationTickets => Set<PreparationTicket>();
    public DbSet<PreparationTicketLine> PreparationTicketLines => Set<PreparationTicketLine>();

    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<DayEndReport> DayEndReports => Set<DayEndReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            await operation();
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
