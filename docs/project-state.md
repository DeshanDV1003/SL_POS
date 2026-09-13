# Universal POS — Project State

Last updated: 2026-09-13, after Phase 7 (core restaurant POS).

## Current Phase
Phase 7 (Restaurant POS) core flow complete and verified end-to-end: open table ->
add items -> route to kitchen/bar stations as KOT/BOT -> KDS status updates -> bill
(reusing the Phase 6 checkout engine, service charge included) -> table released.
Phases 5 and 6 both have documented gaps (see Known Gaps). Awaiting go-ahead for the
next module.

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

- **Phase 6 — Retail POS (core)**: Centralized, unit-tested money math
  (`Domain.Sales.Money`/`SaleLineCalculator`/`PaymentAllocator` — 15 unit tests
  covering VAT-inclusive vs exclusive tax, line discounts, rounding, split payment,
  cash overpayment/change, and the rule that a non-cash instrument can never
  "overpay" since it can't hand back change). `ISalesService.CheckoutAsync`
  authorizes every payment (via a real `IPaymentProvider` abstraction — a real Cash
  provider and a deterministic `SandboxCardPaymentProvider`, never claiming a real
  card was actually charged) BEFORE any database write, so a decline or underpayment
  never leaves a partial sale or a stock deduction behind — verified directly, not
  assumed. A completed sale posts real stock movements and enqueues a
  `FiscalTransmission` row through the Phase 3 fiscal-reporting scaffold. Void
  restores stock via a reversing ledger entry and writes the platform's first real
  `AuditLog` row (that table had existed since Phase 3 with nothing writing to it).
  Hold/recall for suspended carts. Frontend: a full checkout screen (barcode/name
  search, cart with per-line quantity/discount, cash tender with change display,
  hold/recall) plus a post-sale receipt view.
  23 integration tests + 17 unit tests, all passing.

  A third real test-isolation bug surfaced here: the account-lockout test was still
  sacrificing `cashier.lfm`, which Phase 6's tests now needed for legitimate
  checkouts. Fixed by seeding a dedicated `qa.lockouttest` account used by nothing
  else, rather than reusing a "real" seeded user for a destructive test.

- **Phase 7 — Restaurant POS (core)**: Floor/DiningTable/TableSession schema; Order/
  OrderLine as the in-progress pre-bill cart (distinct from SaleHeader, since an order
  accumulates rounds over an evening and tracks per-item kitchen status, neither of
  which apply to a finalized sale); KitchenStation + a single unified PreparationTicket
  model for both KOT and BOT, distinguished only by the station's Category — "same
  architecture, different station" as planned in docs/database-design.md. Sending an
  order to the kitchen groups its pending lines by each product's routed station and
  creates one ticket per station (verified: a 2-item order split correctly into a
  Main-Kitchen ticket and a Bar ticket). Billing an order calls straight into Phase 6's
  `ISalesService.CheckoutAsync` with the order's lines — no duplicated tax/payment
  logic — so it inherits the same payment-authorization-before-any-write guarantee;
  verified directly: an underpayment that ignored the branch's 10% service charge left
  the table Occupied and the order Open, and only the correct payment released the
  table back to Available. Frontend: a floor plan (color-coded table status, tap to
  seat/open an order), an order screen (add items, send to kitchen, bill), and a KDS
  screen (per-station ticket queue with one-tap status advancement).
  28 integration tests + 17 unit tests, all passing.

  One real bug found via testing: `Dictionary<long,long>.GetValueOrDefault` on a
  missing key returns 0 (not null) since the value type isn't nullable, so a table
  with no open order was reporting `OpenOrderId: 0` instead of `null` — the kind of
  bug that reads as "table 0 is open" to a naive client. Fixed with `TryGetValue`.

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

**Within Phase 6, explicitly deferred:** price override at checkout (permission
`sales.price.override` exists but nothing checks it — the line price always comes
from `Product.SellingPrice`), refunds as a distinct flow from void (void is the only
correction path so far), a promotions/coupon engine (only a flat per-line discount
percentage exists), receipt/invoice printing and the "full tax invoice" purchaser-TIN
mode's actual PDF/print output (the `InvoiceMode`/purchaser fields exist on
`SaleHeader` and are validated, but nothing renders the mandated format yet),
`BankTransfer`/`Digital`/`Credit` payment methods have no `IPaymentProvider`
implementation (checkout correctly rejects them with a clear 409 rather than silently
mishandling them). `ApprovalRequest` table still has nothing writing to it — Sale void
uses a direct permission check, not the approval-request workflow.

**Within Phase 7, explicitly deferred:** table merge/split/transfer (TableSession
exists as a 1:1 with an Order; the "relink Order<->Table" mechanism the architecture
doc describes for merge/split isn't implemented), delayed-ticket visual highlighting
in the KDS, KOT/BOT cancellation with a reason and audit trail (`KotCancel`
permission exists, nothing calls it), delivery/takeaway-specific fields (address,
delivery zone/fee — `OrderType` distinguishes them but Takeaway/Delivery orders have
no extra data captured), and printed KOT/BOT tickets (a real kitchen would print
these, not just show them on a KDS screen).

**Everything after Phase 7:** Cash/shift management and Z-reports, CRM/Loyalty
beyond the bare Customer record, Reporting, Offline/sync, Hardware abstraction
implementations, real fiscal/e-invoice provider, real payment gateway integration,
and the Phase 5/6 gaps listed above. ProductComponent/ProductModifierGroup tables
exist but nothing reads them (no checkout or order logic expands a bundle or applies
a modifier's price adjustment yet).

## Architecture Decisions Locked In (see docs/architecture.md for full rationale)
- Modular monolith, not microservices.
- React (not Blazor/Angular) for the POS terminal frontend, for offline/PWA maturity.
- Tax, service charge, and invoice numbering are admin-configurable, never hardcoded —
  reinforced by Sri Lanka's real regulatory churn (VAT rate changes, SVAT abolition,
  new mandatory invoice format, incoming IRD e-invoicing).
- `IFiscalReportingProvider` and `FiscalTransmission` exist now, ahead of most other
  business logic, because Sri Lanka's e-invoicing rollout is an active 2026 program.

## Next Task
Ask the user which to do next: (a) finish Phase 5 (StockTransfer, StockCount,
PurchaseInvoice, SupplierPayment), (b) round out Phase 6 gaps (price override,
refunds, promotions engine, receipt printing), (c) round out Phase 7 gaps (table
merge/split/transfer, KOT/BOT cancellation, delivery/takeaway details), or (d) start
Phase 8 (Cash Management: cashier shifts, cash in/out, Z-report/day-end close).
