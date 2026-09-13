# Universal POS — Project State

Last updated: 2026-09-13, after Phase 5 (partial — see Known Gaps).

## Current Phase
Phase 5 (Inventory + Purchasing) core chain complete and verified end-to-end: PO ->
Approval -> GRN receipt -> StockLedger -> StockOnHand, and a manager-approval-gated
StockAdjustment workflow. StockTransfer, StockCount, PurchaseInvoice, and
SupplierPayment are NOT yet built — see Known Gaps. Awaiting go-ahead for either
finishing the remainder of Phase 5 or moving to Phase 6 (Retail POS) with the
inventory foundation as-is.

## Completed Modules
- **Phase 0–2**: Discovery, architecture (`docs/architecture.md`), database design
  (`docs/database-design.md`), Sri Lanka tax/market research
  (`docs/research-sri-lanka-pos.md`).
- **Phase 3 — Foundation**: Company/Branch/Terminal hierarchy, Role/Permission model,
  JWT auth with rotating refresh tokens + account lockout, policy-based authorization,
  centralized exception handling, audit/approval-request schema (tables exist, not yet
  wired to any write path — no sensitive actions exist yet to audit), FiscalTransmission
  scaffold + no-op `IFiscalReportingProvider`. Backend (.NET 8) and frontend (React 18 +
  TS + Vite) both real and verified against a live SQL Server LocalDB instance, not
  mocked.
- **Phase 4 — Master Data**: Category (self-referencing)/Brand/Unit(with base-unit
  conversion)/TaxRate(VAT vs SCL modeled separately), Product with multi-barcode,
  variants, composite/bundle components, and modifier groups (schema in place; checkout
  logic comes in Phase 6), Supplier/SupplierProduct, Customer/CustomerGroup (Customer
  captures TaxRegistrationNo for the mandatory full-tax-invoice purchaser TIN due 1 Jul
  2026). Real business-rule validation (SKU/barcode/name uniqueness -> 409, price/stock
  sanity checks -> 400) verified via curl against the live API, not just unit tests.
  Product Catalog frontend page with live search.
- **Phase 5 — Inventory + Purchasing (core, partial)**: Immutable StockLedger +
  transactionally-maintained StockOnHand summary (`IStockService.PostMovementAsync`,
  never a bare quantity update), ProductBatch (created automatically on receipt for
  TrackBatches/TrackExpiry products), StockAdjustment with a real manager-approval
  workflow (a requester cannot approve their own adjustment; already-resolved
  adjustments reject a second approval), PurchaseOrder -> GoodsReceivedNote flow that
  posts real stock movements inside one DB transaction spanning two SaveChanges calls
  (`IApplicationDbContext.ExecuteInTransactionAsync`). Verified end-to-end via curl:
  PO created -> approved -> GRN received -> StockLedger row appears -> StockOnHand
  updates; adjustment created by a manager, self-approval correctly blocked with 403,
  admin approval correctly applies the delta, double-approval correctly blocked with
  409. Frontend: a Stock On Hand page.
  17 integration tests + 2 unit tests, all passing.

  Two real bugs found and fixed via this phase's testing (not caught by compiling):
  (1) `JwtBearerOptions` was silently remapping the `sub` claim to a legacy
  `ClaimTypes.NameIdentifier` URI, so `ICurrentUserService.UserId` always returned
  null — invisible until a feature (this phase) first needed UserId rather than just
  CompanyId. Fixed with `options.MapInboundClaims = false`. (2) Enums sent as JSON
  request bodies (e.g. `StockAdjustmentReason`) failed to deserialize because
  System.Text.Json defaults to numeric enum values; fixed by registering
  `JsonStringEnumConverter` globally so the API accepts/returns enum names.

## Solution Layout
```
UniversalPOS.slnx
src/Backend/{UniversalPOS.Domain,Application,Infrastructure,Api}
src/Frontend/pos-web/          (React + TS + Vite)
tests/{UniversalPOS.Domain.Tests,UniversalPOS.IntegrationTests}
docs/
```

## How to Run Locally
1. Backend: `dotnet run --project src/Backend/UniversalPOS.Api` (applies migrations +
   seeds data automatically on startup against the connection string in
   `appsettings.json`, which points at `(localdb)\MSSQLLocalDB`).
2. Frontend: `npm install && npm run dev` inside `src/Frontend/pos-web` (proxies `/api`
   to `http://localhost:5080`).
3. Seeded logins (all password `Passw0rd!`): `admin.cs` / `manager.cs` / `cashier.cs` /
   `kitchen.cs` (Ceylon Spice Restaurant), `admin.lfm` / `manager.lfm` / `cashier.lfm`
   (Lanka Fresh Mart).

## Known Bugs Fixed This Phase (for history — not currently open)
- EF Core `Single*Async` + `AsSplitQuery` + multiple sibling collection includes threw
  "Sequence contains more than one element" even for a genuinely unique row → switched
  to `First*Async` in `AuthService`.
- Terminal login resolution matched terminals by `Code` alone, but `Terminal.Code` is
  only unique per-Branch (every branch may have its own "T1") → login now requires
  `BranchCode` alongside `TerminalCode`.
- `Username` uniqueness was scoped per-Company, but login has no company-selector step
  → changed to a global unique index (documented as a v1 assumption: one Company per
  deployment; a future hosted multi-tenant mode would need to reintroduce a
  company-selector and relax this back to per-Company).

## Known Gaps / Not Yet Implemented
**Within Phase 5, explicitly deferred (not fake — simply not built yet):**
StockTransfer (branch-to-branch), StockCount (physical count reconciliation),
PurchaseInvoice and SupplierPayment (the purchasing module currently stops at GRN —
there is no supplier billing/payment tracking yet), purchase returns. The
PurchaseOrder numbering scheme (`PO-{branchId}-{count+1:D6}`) is a simple counter, not
a concurrency-safe sequence generator — two concurrent order creations on the same
branch could theoretically race to the same number; a dedicated NumberSequence table
with proper locking is the correct fix before this goes to production concurrency.

**Everything after Phase 5:** Retail POS checkout (no sale/payment path exists yet —
Product, StockOnHand, TaxRate all exist but nothing ties them together into a
transaction), Restaurant POS/KOT/BOT/KDS, Payments, Cash management, CRM/Loyalty
beyond the bare Customer record, Promotions, Reporting, Offline/sync, Hardware
abstraction implementations, real fiscal/e-invoice provider, real payment gateway
integration. AuditLog/ApprovalRequest tables exist but nothing writes to them yet.
ProductComponent/ProductModifierGroup tables exist but nothing reads them (no checkout
logic to expand a bundle or apply a modifier's price adjustment).

## Architecture Decisions Locked In (see docs/architecture.md for full rationale)
- Modular monolith, not microservices.
- React (not Blazor/Angular) for the POS terminal frontend, for offline/PWA maturity.
- Tax, service charge, and invoice numbering are admin-configurable, never hardcoded —
  reinforced by Sri Lanka's real regulatory churn (VAT rate changes, SVAT abolition,
  new mandatory invoice format, incoming IRD e-invoicing).
- `IFiscalReportingProvider` and `FiscalTransmission` exist now, ahead of most other
  business logic, because Sri Lanka's e-invoicing rollout is an active 2026 program.

## Next Task
Either (a) finish Phase 5 — StockTransfer, StockCount, PurchaseInvoice,
SupplierPayment — or (b) proceed to Phase 6 (Retail POS: checkout, cart, discounts,
split payment, holds/recalls) using the inventory/purchasing foundation already built.
Ask the user which before starting.
