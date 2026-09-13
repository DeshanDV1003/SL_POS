# Universal POS — Phase 2: Database Design

Status: DRAFT for checkpoint approval
Engine: SQL Server. Naming: PascalCase tables/columns, singular table names, surrogate
`Id BIGINT IDENTITY` primary keys everywhere except pure join tables, `RowVersion
ROWVERSION` on any row subject to concurrent update, `CreatedAtUtc` / `CreatedByUserId`
/ `ModifiedAtUtc` / `ModifiedByUserId` audit columns on all business tables, soft delete
(`IsDeleted BIT`, `DeletedAtUtc`) only where hiding history is legitimate (e.g. a
discontinued Product) — never on ledger/transaction rows, which are immutable.

This document details the **Foundation schema** (Phase 3 scope) fully, since that is
what gets built next, and outlines the schema shape for later phases so the overall
ERD is coherent. Each later phase's own checkpoint will finalize its exact DDL.

## 1. Foundation Schema (Phase 3 — full detail)

### Company
`Id, Name, LegalName, BusinessRegistrationNo, TaxRegistrationNo (TIN), DefaultCurrencyCode
(char(3), default 'LKR'), TimeZone, IsActive, CreatedAtUtc, ...`
One row per deployed business (single-tenant v1; `TenantId` column reserved, nullable,
for future hosted multi-tenant mode).

### Branch
`Id, CompanyId (FK), Name, Code (unique per Company), Address, City, Phone,
BusinessTypeFlags (int bitmask: Restaurant=1, Retail=2, Grocery=4, Wholesale=8, ...),
IsActive`
A Branch's `BusinessTypeFlags` gates which modules (tables/KOT, barcode retail
checkout, wholesale pricing tiers) are active for that branch.

### Terminal
`Id, BranchId (FK), Name, Code (unique per Branch), DeviceIdentifier, IsActive,
LastSeenAtUtc`
Represents a physical POS device/register; terminal-level session tokens are issued
against this row (§ Architecture doc 5).

### AppUser
`Id, CompanyId (FK), Username, Email (nullable), PhoneNumber (nullable),
PasswordHash, Pin (hashed, optional, for POS quick-login), FullName, IsActive,
IsLockedOut, FailedLoginCount, LockedOutUntilUtc, LastLoginAtUtc`

### UserBranch (join)
`UserId (FK), BranchId (FK)` — composite PK. Defines which branches a user may operate
in; enforced as a query filter alongside role permissions.

### Role
`Id, CompanyId (FK, nullable for system-seeded global roles), Name, IsSystemRole`

### Permission
`Id, Code (unique, e.g. "sales.void"), Description, Category` — seeded, not
user-editable (new permissions ship with code updates, not admin UI).

### RolePermission (join)
`RoleId (FK), PermissionId (FK)` — composite PK.

### UserRole (join)
`UserId (FK), RoleId (FK)` — composite PK.

### RefreshToken
`Id, UserId (FK), TerminalId (FK, nullable), TokenHash, ExpiresAtUtc, IsRevoked,
ReplacedByTokenId (nullable), CreatedAtUtc, CreatedByIp`

### AuditLog
`Id, CompanyId, BranchId (nullable), TerminalId (nullable), UserId, ActionCode
(e.g. "Sale.Void", "Product.PriceChanged"), EntityType, EntityId, OldValueJson
(nullable), NewValueJson (nullable), CreatedAtUtc, IpAddress`
Immutable, append-only, indexed on `(EntityType, EntityId)` and `(CreatedAtUtc)` for
investigation queries. `OldValueJson`/`NewValueJson` are the one legitimate JSON use —
a diff snapshot, not queryable business data.

### ApprovalRequest
`Id, RequestedByUserId, ApprovedByUserId (nullable), ActionCode, EntityType, EntityId,
Status (Pending/Approved/Rejected), ReasonNote, CreatedAtUtc, ResolvedAtUtc`
Backs the manager-approval workflow for sensitive actions.

### FiscalTransmission
`Id, CompanyId, BranchId, SaleHeaderId (FK), Status (Pending/Sent/Acknowledged/Failed),
Payload (nvarchar(max), the exact submission sent), ProviderResponse (nullable),
AttemptCount, LastAttemptAtUtc, CreatedAtUtc`
One row per finalized sale, enqueued regardless of whether a real
`IFiscalReportingProvider` is wired up (§ Architecture 9). With the default
`NullFiscalReportingProvider`, rows go straight to `Acknowledged` with a "no-op" note —
this keeps the queue/table live and tested well before a real RAMIS integration
exists, so turning it on later is a provider swap, not a schema change.

## 2. Master Data Schema (Phase 4 — outline)

- `Category` (self-referencing `ParentCategoryId` for subcategories), `Brand`, `Unit`
  (each with `BaseUnitId` + conversion factor for e.g. kg↔g).
- `Product` (SKU, Barcode as separate `ProductBarcode` 1-to-many table for multi-barcode
  support, CostPrice, SellingPrice, WholesalePrice, MinSellingPrice, ReorderLevel,
  MinStock, MaxStock, TrackBatches bool, TrackExpiry bool, TrackSerial bool,
  IsComposite bool), `ProductVariant` (size/color-style variants sharing a parent
  Product), `ProductModifierGroup`/`ProductModifier` (add-ons, e.g. "extra cheese"),
  `ProductComponent` (self-referencing, for bundles/recipes — a composite Product's bill
  of materials against component Products, with quantity, used to deduct stock
  correctly at sale time).
- `Supplier`, `SupplierProduct` (preferred supplier + cost per Product).
- `Customer`, `CustomerGroup`, `TaxRate` (see Architecture §9), `ProductTax` (join,
  Product↔TaxRate, allows category-level default with product-level override).

## 3. Inventory & Purchasing Schema (Phase 5 — outline)

- `StockLedger` (immutable): `Id, BranchId, ProductId, VariantId (nullable), BatchId
  (nullable), MovementType (Purchase/Sale/Return/Transfer/Adjustment/Wastage/Opening),
  QuantityChange (signed decimal), ReferenceType, ReferenceId, CreatedAtUtc,
  CreatedByUserId`. Current stock is a **derived aggregate** (materialized/indexed view
  or a maintained `StockOnHand` summary table updated transactionally alongside every
  ledger insert) — never the sole source of truth.
- `ProductBatch` (batch/lot number, expiry date, received date, remaining quantity).
- `StockTransfer` / `StockTransferLine` (branch-to-branch, with Requested/Sent/Received
  status workflow).
- `StockAdjustment` / `StockAdjustmentLine` (reason code: Wastage/Damage/Expiry/Count
  discrepancy/Other, requires a permission + optionally an ApprovalRequest).
- `StockCount` / `StockCountLine` (physical count sessions reconciled against system
  quantity, discrepancies posted as adjustments).
- `Supplier`-side: `PurchaseRequest`, `PurchaseOrder`/`PurchaseOrderLine`,
  `GoodsReceivedNote`/`GoodsReceivedNoteLine`, `PurchaseInvoice`, `PurchaseReturn`,
  `SupplierPayment`.

## 4. Sales / Retail POS Schema (Phase 6 — outline)

- `SaleHeader` (immutable once finalized): `Id, BranchId, TerminalId, CashierUserId,
  CustomerId (nullable), InvoiceNumber (sequential per Branch, server-assigned, gapless),
  SaleType (Retail/Restaurant/Wholesale), Status (Held/Completed/Voided/Refunded),
  InvoiceMode (SimplifiedReceipt/FullTaxInvoice — full mode captures purchaser
  TIN/name/address per the 1 Jul 2026 mandated format), PurchaserTin (nullable),
  PurchaserName (nullable), PurchaserAddress (nullable), SubTotal, DiscountTotal,
  TaxTotal, ServiceChargeTotal, GrandTotal, RoundingAdjustment, ClientIdempotencyKey
  (for offline sync dedup), SyncedAtUtc (nullable)`.
- `SaleLine` (`ProductId, VariantId, Quantity, UnitPrice, LineDiscount, LineTax,
  LineTotal, KotStatus nullable FK for restaurant flow`).
- `SalePayment` (`SaleHeaderId, PaymentMethod, Amount, ProviderReference,
  ProviderStatus`).
- `SaleVoid`/`SaleRefund` as append-only records referencing the original SaleHeader —
  a void/refund is a new linked record, never a mutation of the original sale.
- `HeldBill` (suspended sale before checkout, separate from a finalized SaleHeader).
- `Promotion`/`PromotionRule`/`Coupon` (discount engine — condition + effect pairs,
  priority ordering for conflict resolution).

## 5. Restaurant Schema (Phase 7 — outline)

- `Floor`, `Table` (`Status`: Available/Occupied/Reserved/Cleaning/Billing,
  `Capacity`), `TableSession` (opens when a table is seated, links to an in-progress
  Order, closes on bill payment — supports merge/split/transfer by relinking
  Order↔Table).
- `KitchenStation` (e.g. Main Kitchen, Bar, Dessert), `Order`/`OrderLine` (the
  in-progress restaurant order before it becomes a SaleHeader at billing time),
  `Kot`/`KotLine` (routed to a KitchenStation, `Status`:
  Pending/Sent/Accepted/Preparing/Ready/Served/Cancelled, `KotNumber` sequential per
  Branch per day), `Bot`/`BotLine` (same shape, Bar-routed) — implemented as the *same*
  underlying `PreparationTicket` table type-discriminated by `StationCategory` per
  Architecture's "same architecture, different station" note, rather than duplicated
  tables.

## 6. Cash / Payments / CRM / Reporting (Phases 8–10 — outline)

- `CashierShift` (`OpeningFloat, ClosingFloatCounted, ExpectedCash, VarianceAmount,
  Status, OpenedAtUtc, ClosedAtUtc`), `CashMovement` (Cash In/Out/Petty within a
  shift), `DayEndReport` (finalized Z-report snapshot, immutable once
  `Status = Finalized`).
- `Customer` loyalty fields (`LoyaltyPointsBalance`, `MembershipTierId`),
  `LoyaltyTransaction` (immutable ledger, mirroring `StockLedger`'s pattern — points
  are never mutated in place).
- Reporting is built on indexed views / query projections over the above, not separate
  duplicated report tables, to guarantee reports reflect real ledger data.

## 7. Indexing & Constraints Approach

- Every FK indexed. `Product.Barcode` (in `ProductBarcode`) unique per Company.
  `SaleHeader.InvoiceNumber` unique per `(BranchId, InvoiceNumber)`.
  `Kot.KotNumber` unique per `(BranchId, BusinessDate, KotNumber)`.
- `CHECK` constraints for non-negative quantities/prices where domain-invalid
  otherwise (e.g. `Quantity <> 0` on `StockLedger`, `GrandTotal >= 0` on
  `SaleHeader` unless `SaleType = Refund`).
- Global query filter on every Company-scoped entity (`CompanyId == currentCompanyId`)
  applied via EF Core, so a missed `.Where()` clause can't leak cross-tenant data.

## 8. Migration & Seed Strategy

- EF Core Code-First migrations, one migration per meaningful schema change, named
  descriptively (`20260913_AddForeignKeyCompanyBranch`, not `Migration1`).
- Seed data (Phase 3 onward): a sample Company "Ceylon Spice Restaurant (Pvt) Ltd"
  (restaurant) and a sample Company "Lanka Fresh Mart" (supermarket/retail) with
  realistic branches, products (rice, dhal curry, kottu, string hoppers; groceries with
  real-looking barcodes), suppliers, and a handful of users per role — used for
  development and demo, replaceable per real deployment.

Next step once this and the architecture doc are approved: scaffold the actual
solution (`UniversalPOS.sln`, the four backend projects, first EF Core migration for
the Foundation schema above) — Phase 3.
