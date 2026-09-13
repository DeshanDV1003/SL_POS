namespace UniversalPOS.Domain.Identity;

/// <summary>
/// Canonical permission codes. New permissions ship with code changes, not an admin UI —
/// business logic checks these via policy-based authorization, never a hardcoded role name.
/// </summary>
public static class PermissionCodes
{
    // Sales
    public const string SalesCreate = "sales.create";
    public const string SalesVoid = "sales.void";
    public const string SalesRefund = "sales.refund";
    public const string SalesDiscountApply = "sales.discount.apply";
    public const string SalesPriceOverride = "sales.price.override";
    public const string SalesReprint = "sales.reprint";

    // Cash
    public const string CashDrawerOpen = "cash.drawer.open";
    public const string CashShiftOpen = "cash.shift.open";
    public const string CashShiftClose = "cash.shift.close";
    public const string CashMovementCreate = "cash.movement.create";
    public const string DayEndReportFinalize = "cash.dayend.finalize";

    // Inventory
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryTransfer = "inventory.transfer";
    public const string InventoryCount = "inventory.count";

    // Products / catalog
    public const string ProductManage = "product.manage";
    public const string TaxRateManage = "tax.rate.manage";

    // Purchasing
    public const string PurchaseOrderCreate = "purchase.order.create";
    public const string PurchaseOrderApprove = "purchase.order.approve";
    public const string GoodsReceiptCreate = "purchase.grn.receive";
    public const string SupplierManage = "supplier.manage";
    public const string StockAdjustmentApprove = "inventory.adjust.approve";

    // CRM
    public const string CustomerManage = "customer.manage";

    // Reports
    public const string ReportsViewSales = "reports.view.sales";
    public const string ReportsViewFinancial = "reports.view.financial";

    // Admin
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";
    public const string CompanyManage = "company.manage";
    public const string BranchManage = "branch.manage";
    public const string TerminalManage = "terminal.manage";
    public const string AuditView = "audit.view";

    // Restaurant
    public const string TableManage = "restaurant.table.manage";
    public const string KotCancel = "restaurant.kot.cancel";
    public const string OrderCreate = "restaurant.order.create";
    public const string KdsUpdate = "restaurant.kds.update";
    public const string OrderBill = "restaurant.order.bill";

    public static readonly IReadOnlyList<(string Code, string Category, string Description)> All = new[]
    {
        (SalesCreate, "Sales", "Create a sale"),
        (SalesVoid, "Sales", "Void a completed sale"),
        (SalesRefund, "Sales", "Issue a refund"),
        (SalesDiscountApply, "Sales", "Apply a discount"),
        (SalesPriceOverride, "Sales", "Override a product's selling price at checkout"),
        (SalesReprint, "Sales", "Reprint a receipt/invoice"),
        (CashDrawerOpen, "Cash", "Open the cash drawer outside of a sale"),
        (CashShiftOpen, "Cash", "Open a cashier shift"),
        (CashShiftClose, "Cash", "Close a cashier shift"),
        (CashMovementCreate, "Cash", "Record cash in/out/petty cash"),
        (DayEndReportFinalize, "Cash", "Finalize (lock) a day-end Z-report"),
        (InventoryAdjust, "Inventory", "Post a stock adjustment"),
        (InventoryTransfer, "Inventory", "Create/approve a stock transfer"),
        (InventoryCount, "Inventory", "Perform a stock count"),
        (ProductManage, "Catalog", "Create/edit products, categories, brands, and units"),
        (TaxRateManage, "Catalog", "Create/edit tax rates"),
        (PurchaseOrderCreate, "Purchasing", "Create a purchase order"),
        (PurchaseOrderApprove, "Purchasing", "Approve a purchase order"),
        (GoodsReceiptCreate, "Purchasing", "Record goods received from a supplier"),
        (SupplierManage, "Purchasing", "Create/edit suppliers"),
        (StockAdjustmentApprove, "Inventory", "Approve a pending stock adjustment"),
        (CustomerManage, "CRM", "Create/edit customers and customer groups"),
        (ReportsViewSales, "Reports", "View sales reports"),
        (ReportsViewFinancial, "Reports", "View financial reports"),
        (UserManage, "Admin", "Manage users"),
        (RoleManage, "Admin", "Manage roles and permissions"),
        (CompanyManage, "Admin", "Manage company settings"),
        (BranchManage, "Admin", "Manage branches"),
        (TerminalManage, "Admin", "Manage terminals"),
        (AuditView, "Admin", "View audit logs"),
        (TableManage, "Restaurant", "Manage floors/tables"),
        (KotCancel, "Restaurant", "Cancel a KOT/BOT"),
        (OrderCreate, "Restaurant", "Open a table/order and add items"),
        (KdsUpdate, "Restaurant", "Update a kitchen ticket's preparation status"),
        (OrderBill, "Restaurant", "Bill an order and take payment"),
    };
}
