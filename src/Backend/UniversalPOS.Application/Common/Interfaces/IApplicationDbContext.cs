using Microsoft.EntityFrameworkCore;
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

    DbSet<StockLedger> StockLedgers { get; }
    DbSet<StockOnHand> StockOnHands { get; }
    DbSet<ProductBatch> ProductBatches { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    DbSet<StockAdjustmentLine> StockAdjustmentLines { get; }

    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<GoodsReceivedNote> GoodsReceivedNotes { get; }
    DbSet<GoodsReceivedNoteLine> GoodsReceivedNoteLines { get; }

    DbSet<SaleHeader> SaleHeaders { get; }
    DbSet<SaleLine> SaleLines { get; }
    DbSet<SalePayment> SalePayments { get; }
    DbSet<HeldBill> HeldBills { get; }
    DbSet<HeldBillLine> HeldBillLines { get; }

    DbSet<Floor> Floors { get; }
    DbSet<DiningTable> DiningTables { get; }
    DbSet<TableSession> TableSessions { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderLine> OrderLines { get; }
    DbSet<KitchenStation> KitchenStations { get; }
    DbSet<PreparationTicket> PreparationTickets { get; }
    DbSet<PreparationTicketLine> PreparationTicketLines { get; }

    DbSet<CashierShift> CashierShifts { get; }
    DbSet<CashMovement> CashMovements { get; }
    DbSet<DayEndReport> DayEndReports { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction (with the
    /// provider's retry-on-transient-failure strategy applied around the whole thing),
    /// so a multi-step business operation that needs more than one SaveChangesAsync
    /// call — e.g. saving a GRN to get its generated Id, then posting stock lines that
    /// reference it — commits or rolls back as a single unit rather than leaving the
    /// database in a partially-applied state if a later step fails.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);
}
