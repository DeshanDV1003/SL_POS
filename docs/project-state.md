# Universal POS — Project State

Last updated: 2026-09-13, after Phase 10 (core reporting + dashboard).

## Current Phase
Phase 10 (Reporting) core set complete and verified end-to-end: every report is a
real aggregation query over the same SaleHeader/SaleLine/SalePayment/StockOnHand data
every other module writes (never a separately tracked shadow total) — verified
directly by making a real sale and confirming the summary/by-product/by-payment-method
reports changed by exactly the expected amount. Phases 5-9 all have documented gaps
(see Known Gaps). Awaiting go-ahead for the next module.

## Repository
This project is now connected to a GitHub remote: `origin` ->
https://github.com/DeshanDV1003/SL_POS.git, branch `main` (the remote was empty when
connected, so no history conflict). Git author identity (Deshan Vimukthi De Silva,
dvdsilva@students.nsbm.ac.lk) is already configured globally on this machine — no
config changes were made or are needed. Pushing to the remote is blocked by this
session's auto-mode safety classifier (an external, visible action); the user pushes
manually with `git push -u origin main` from a normal terminal, or by taking this
session out of auto mode.

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

- **Phase 8 — Cash Management (core)**: CashierShift (one open shift per terminal and
  per user, enforced — verified a second open attempt on either axis returns 409),
  CashMovement (cash in/out/petty within a shift), and a real Z-report
  (`DayEndReport`) aggregated from the same SaleHeader/SalePayment data every other
  report would use, not a separately-tracked shadow total. Closing a shift computes
  `ExpectedCash = OpeningFloat + cash sales during the shift + CashIn - CashOut -
  Petty` and the variance against what was actually counted — verified directly with
  a real sale and a real petty-cash movement, not just asserted in isolation. A
  finalized Z-report is locked: verified that a new sale made after finalizing a
  day's report does not change that report's totals on a subsequent fetch, only a
  fresh (different-date) report would reflect it. `SaleHeader` now carries an
  optional `CashierShiftId`, attached automatically at checkout when the cashier has
  one open on that terminal — attachment is best-effort, not mandatory (see gaps).
  Frontend: a Cash Management page (open/close shift, record movements, generate and
  finalize the Z-report).
  32 integration tests + 17 unit tests, all passing.

  No new production bug this phase, but a real test-design mistake was caught before
  it caused flakiness: three cash tests each opening a shift would have collided on
  the same seeded terminal and the same user's "one open shift" rule. Fixed by giving
  each shift-opening test its own terminal index and, where needed, its own user.

- **Phase 9 — CRM + Loyalty (core)**: LoyaltyTransaction (immutable, append-only —
  Customer.LoyaltyPointsBalance is a derived summary updated only alongside a ledger
  row, exactly like StockLedger/StockOnHand) and MembershipTier, auto-assigned by
  current point balance whenever it changes (verified: a sale earning 16 points
  correctly moved a customer from no tier into a "Gold" tier with MinimumPoints=10,
  and manually adding 50 more points kept it there). Points earn automatically at
  checkout when a sale is attached to a customer (1 point per LKR 100 spent, times the
  tier's multiplier) — best-effort, not mandatory. A manual adjustment requires
  `customer.loyalty.adjust` and writes a real AuditLog entry (verified directly in the
  database, not just asserted through the API) — the second sensitive action in this
  codebase to exercise that table, after Phase 6's sale void. Over-redemption is
  rejected with 409. A basic Promotion engine (percentage or fixed-amount, scoped to
  all products/a category/a specific product, date-ranged, priority-ordered) is
  wired into `SalesService.CheckoutAsync`: the larger of the cashier's manual line
  discount and the best-matching active promotion applies — never stacked — verified
  directly that a 15% category promotion overrode a smaller 2% manual discount.
  Frontend: a Customers page (list, create, loyalty balance/tier display).
  37 integration tests + 17 unit tests, all passing.

- **Phase 10 — Reporting + Dashboard (core)**: Sales summary (gross/discount/tax/
  net/void-count/average), sales by product, by category, by cashier, by payment
  method, a stock valuation report (quantity-on-hand x cost price per product), and a
  management dashboard (today's/7-day net sales, low-stock count, top 5 products) —
  all real aggregation queries with a date range (defaulting to the last 7 days), not
  a separately maintained summary table. Verified directly: making a real sale and
  re-fetching the summary showed net sales increase by exactly that sale's grand
  total and the transaction count by exactly 1, and sales-by-payment-method
  correctly separated a card sale from prior cash sales. Permissions split as
  `reports.view.sales` (summary/by-product/by-category, and the dashboard) vs.
  `reports.view.financial` (by-cashier, by-payment-method, stock-valuation) —
  verified a cashier is blocked from all of them and a manager holding both can see
  everything. Frontend: a Reports & Dashboard page.
  43 integration tests + 17 unit tests, all passing.

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

**Within Phase 8, explicitly deferred:** a shift is not mandatory to complete a sale
(checkout works with or without one open — a real deployment likely wants to require
it), `CashDrawerOpen` permission exists but nothing calls it (no hardware-drawer
trigger endpoint yet — that's Phase 12's hardware abstraction), the "business day"
boundary for a Z-report is a plain UTC calendar date rather than a configurable
cutoff time (a sale at 12:30am would fall on the next calendar day even if the
business considers that "still last night"), and there's no report listing/history
endpoint (only get-by-date and finalize).

**Within Phase 9, explicitly deferred:** redemption is a standalone action (deduct
points, e.g. for a physical reward) and does NOT yet apply as a discount/payment
within checkout — a "pay with points" flow at the register isn't built. Points expiry
(the `Expired` transaction type exists on the enum, nothing generates one — no
scheduled job walks old transactions). The earn rate (1 point per LKR 100) is a
hardcoded constant, not per-company configurable. Coupon codes (a customer typing in
a promo code) aren't modeled — only automatic, rule-matched promotions exist.
Promotion matching does one DB query per sale line (fine at SMB cart sizes, a
documented N+1-shaped inefficiency at large cart sizes).

**Within Phase 10, explicitly deferred:** dead/slow-moving stock analysis (needs
historical comparison over time, not just a point-in-time snapshot), a discount
report and a dedicated tax report as separate views (the numbers exist inside sales
summary but aren't broken out into their own report), waiter/kitchen-performance
reports (average prep time, cancelled-KOT counts — the PreparationTicket data exists
from Phase 7 but nothing aggregates it yet), branch-comparison and revenue-trend
charts (only single-branch, non-trended numbers exist), and CSV/PDF export (reports
are JSON API responses only, no download format).

**Everything after Phase 10:** Offline/sync, Hardware abstraction implementations,
real fiscal/e-invoice provider, real payment gateway integration, and the Phase
5/6/7/8/9 gaps listed above.

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
refunds, receipt printing), (c) round out Phase 7 gaps (table merge/split/transfer,
KOT/BOT cancellation, delivery/takeaway details), (d) round out Phase 8 gaps
(mandatory shift-to-sell, configurable business-day cutoff), (e) round out Phase 9
gaps (pay-with-points at checkout, points expiry job, coupon codes), (f) round out
Phase 10 gaps (trend charts, exports, kitchen-performance reports), or (g) start
Phase 11 (Offline + Synchronization).
