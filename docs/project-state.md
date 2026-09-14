# Universal POS — Project State

Last updated: 2026-09-14, after rounding out Phase 7's gaps (table merge/split/
transfer, KOT/BOT cancellation, delivery/takeaway details).

## Current Phase
Phase 7 is now functionally complete: a table can be transferred to another table
(verified: old table freed, new table occupied, order follows), two open orders can
be merged (verified: lines re-parented, source table freed, source order marked
Cancelled without deleting its history), an order can be split across a subset of its
lines onto a new table (verified: remaining lines stay on the original order, moved
lines appear on the new one), standalone Takeaway/Delivery orders can be created
without a physical table (Delivery requires an address), and a KOT/BOT ticket can be
cancelled with a reason that writes a real audit log entry (verified directly in the
database) — the `restaurant.kot.cancel` permission finally has an action behind it.
Phases 5, 6, 8, 9, 10 all have documented gaps (see Known Gaps). Awaiting go-ahead for
the next round of gap-filling.

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

- **Phase 5 gap-fill — StockTransfer, StockCount, PurchaseInvoice, SupplierPayment**:
  StockTransfer (Requested/Sent/Received) posts a real TransferOut ledger movement at
  the source branch only on Send and a real TransferIn movement at the destination
  only on Receive — verified directly: stock at the source dropped by exactly the
  transferred quantity on Send, and stock at the destination (zero beforehand) rose
  by exactly that quantity only after Receive, not before. StockCount snapshots
  StockOnHand into per-line SystemQuantity at creation, and completing the count
  posts a real correcting ledger entry (movement type StockCount) for every line
  where CountedQuantity differs — verified a 3-unit discrepancy correctly reduced
  StockOnHand by exactly 3; completing a count with any line still uncounted is
  rejected (409). PurchaseInvoice/SupplierPayment: GrandTotal computed from
  SubTotal+TaxTotal, AmountPaid is a derived summary updated only alongside a
  SupplierPayment row, status transitions Unpaid -> PartiallyPaid -> Paid — verified
  directly with a real partial payment, a rejected overpayment attempt (400), and a
  final payment that brought the invoice to Paid.
  47 integration tests + 17 unit tests, all passing. No frontend screens were added
  for these (back-office/warehouse operations, judged lower priority than POS-terminal
  UI given the session's remaining scope) — the API is real and tested, but there is
  no UI for it yet.

- **Phase 6 gap-fill — price override, refunds, receipt rendering**: A line's
  `unitPriceOverride` requires `sales.price.override` (verified: a cashier without it
  gets 403, an admin with it can sell below list price) and can never go below
  `Product.MinSellingPrice` regardless of who's asking — a hard floor, not a
  permission-gated one. `RefundSaleAsync` creates a genuinely distinct new SaleHeader
  (Status=Refunded, a "CN-" credit-note number) rather than mutating the original —
  verified directly: refunding 1 of 3 units restored exactly 1 unit of stock and left
  the original sale untouched at Status=Completed. Refund quantity is tracked against
  everything already refunded for that sale (not just the original quantity), so a
  second refund attempt for an already-fully-refunded line is correctly rejected
  (409); the refund's payment lines must sum to exactly the refund amount (400 if
  not) — no "change" concept applies to money going back out. `IReceiptRenderer`
  produces real formatted text for both a simplified receipt and (verified via a
  purchaser-TIN/name/address round-trip) the mandated full-tax-invoice layout,
  exposed at `GET /branches/{id}/sales/{id}/receipt`. Frontend: a "View/Print Receipt"
  button on the post-sale screen that fetches and displays the real rendered text.
  55 integration tests + 17 unit tests, all passing.

- **Phase 7 gap-fill — table merge/split/transfer, KOT/BOT cancellation, delivery
  details**: `TransferTableAsync` moves a TableSession to a new table, freeing the
  old one — verified directly. `MergeOrdersAsync` re-parents OrderLines (not copies —
  each line's KOT ticket history stays attached) into a target order and marks the
  source Cancelled, freeing its table. `SplitOrderAsync` opens a fresh TableSession
  on a chosen table and moves only the selected OrderLines there, leaving the rest on
  the original order — verified both sides end up with exactly the right lines.
  `CreateStandaloneOrderAsync` supports Takeaway/Delivery orders with no physical
  table, and Delivery requires an address (verified: 400 without one).
  `CancelTicketAsync` requires `restaurant.kot.cancel`, marks the ticket and its
  OrderLines Cancelled, and writes a real AuditLog row (`Kot.Cancel`) — verified
  directly in the database, not just via the API response — a second cancel attempt
  on the same ticket is rejected (409).
  61 integration tests + 17 unit tests, all passing.

  One real test-design bug found via testing (not a production bug): Ceylon Spice's
  seeded 4 dining tables were exhausted once enough restaurant tests ran in the same
  collection-shared database, causing later tests to fail finding an "Available"
  table with "Sequence contains no matching element." Fixed by seeding 20 tables — a
  more realistic count for an actual restaurant anyway, not just a test workaround.

- **Phase 8 gap-fill — mandatory shift-to-sell, business-day cutoff, report history,
  cash-drawer audit action**: `Branch.RequireOpenShiftForSale` (off by default, so
  existing/simpler deployments aren't forced into shift discipline) makes
  `CheckoutAsync` throw a 409 when the cashier has no open shift on the terminal —
  verified directly: the same checkout request that gets rejected with the flag on
  succeeds once a shift is opened, and is unaffected when the flag is off.
  `Branch.BusinessDayCutoffHour` (0-23, validated) shifts the Z-report's day boundary
  away from plain UTC midnight — verified by setting the cutoff to the current hour
  and confirming a sale made right after is attributed to *today's* business date but
  not yet visible under *tomorrow's* (whose window hasn't opened). Both settings are
  updated via `PUT /branches/{id}/cash-settings`, gated by `branch.manage` (verified:
  a manager without it gets 403). `GET /branches/{id}/day-end-report/history` lists
  all previously generated reports for a branch, newest business date first.
  `POST /branches/{id}/cash-drawer/open`, gated by `cash.drawer.open`, writes a real
  `AuditLog` row (`ActionCode = "Cash.DrawerOpen"`) — honestly, there's still no
  physical drawer to trigger (that's Phase 12's hardware abstraction), but the
  permission-gated action and its audit trail are real and verified directly against
  the database. 69 integration tests + 17 unit tests, all passing.

  One real test-isolation bug found via testing (not a production bug): a new test
  that opened a cashier shift with `admin.lfm` and didn't close it left that user
  permanently "busy" — `CashierShift`'s open-shift constraint is enforced globally per
  user, not per-branch or per-terminal — which broke unrelated `CashEndpointTests`
  cases later in the same shared test-collection database that also use `admin.lfm`
  to open a shift. Fixed by closing the shift in a `finally` block, matching the
  pattern already used by the well-behaved existing tests. Separately, ad-hoc manual
  `curl` verification against the shared `UniversalPosTests` LocalDB left behind a
  stale invoice-number sequence collision; recreating the test database (migrations +
  seed re-run automatically on next startup) cleared it — not a product bug.

- **Phase 9 gap-fill — pay-with-points, coupon codes, per-company loyalty rates, points
  expiry**: `PaymentMethod.LoyaltyPoints` is handled entirely inside `SalesService`
  rather than through `IPaymentProvider` (it needs the sale's CustomerId, which
  `PaymentAuthorizationRequest` doesn't carry, and the ledger write must happen inside
  the sale's own transaction) — verified: a customer with enough points can pay part of
  a sale with them (a real `Redeemed` LoyaltyTransaction referencing the sale, balance
  deducted), a sale attempted without a customer is rejected 400, and one that exceeds
  the customer's balance is rejected 409. `Domain.Sales.Coupon` supports customer-typed
  codes, applied as a flat reduction to `GrandTotal` (a documented v1 simplification —
  like a manufacturer coupon, it doesn't redistribute across lines or change the tax
  base) — verified the discount percentage against the pre-coupon total, the coupon's
  `TimesRedeemed` incrementing, and 404/409 for an unknown/expired/limit-reached code.
  `Company.LoyaltyPointsPerCurrencyUnit` and `LoyaltyPointRedemptionValue` replace the
  old hardcoded 1-point-per-LKR-100 constant, updated via
  `PUT /companies/{id}/loyalty-settings` (gated by `company.manage`, verified 403 for a
  manager) — verified a changed earn rate actually changes the points a real checkout
  earns. `LoyaltyTransaction.ExpiresAtUtc`/`IsExpired` and
  `ILoyaltyService.ExpirePointsAsync` implement expiry as a **v1 simplification**: it
  expires at the customer-balance level (capped at their current balance) rather than
  tracking each Earned batch's remaining points through FIFO redemption consumption —
  a real system would need that for exact correctness, but this is honest, safe (never
  goes negative), and real (posts an actual `Expired` ledger row, marks batches
  processed so a rerun is a no-op). A `PointsExpiryBackgroundService` runs it daily for
  every active company; `POST /companies/{id}/loyalty/expire-points` runs it on demand
  (used by tests, so this doesn't require waiting a day to verify) — verified
  end-to-end by backdating a real Earned batch's `ExpiresAtUtc` (via direct DbContext
  access in the test, the same way a live verification would reach into the real
  database) and confirming the balance dropped exactly once, not twice on a rerun.
  79 integration tests + 17 unit tests, all passing.

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
**Within Phase 5, still remaining after the gap-fill:** purchase returns (sending
damaged/wrong goods back to a supplier) still isn't modeled. The PurchaseOrder
numbering scheme (`PO-{branchId}-{count+1:D6}`) is a simple counter, not a
concurrency-safe sequence generator — two concurrent order creations on the same
branch could theoretically race to the same number; a dedicated NumberSequence table
with proper locking is the correct fix before this goes to production concurrency
(the same is true of the new StockTransfer/PurchaseInvoice flows, which don't even
have their own display numbers yet — only a database Id). No frontend UI exists yet
for stock transfer, stock count, or supplier invoicing/payment — they are real,
tested APIs without a screen.

**Within Phase 6, still remaining after the gap-fill:** a coupon engine (customer-
entered codes; the automatic Promotion engine from Phase 9 is unrelated and already
built), PDF rendering of the receipt (it's real formatted plain text sized for an
80mm thermal printer, not a PDF/HTML document — appropriate for the hardware target,
but there's no "email a PDF invoice" path), `BankTransfer`/`Digital`/`Credit` payment
methods still have no `IPaymentProvider` implementation (checkout correctly rejects
them with a clear 409 rather than silently mishandling them), and a refund still
requires the exact original line's product to be looked up by ProductId — a fully
custom refund line (e.g. issuing store credit unrelated to any original line) isn't
supported. `ApprovalRequest` table still has nothing writing to it — Sale
void/refund use direct permission checks, not the approval-request workflow.

**Within Phase 7, still remaining after the gap-fill:** delayed-ticket visual
highlighting in the KDS (elapsed time isn't computed or flagged), a delivery
zone/driver-assignment model (`DeliveryFee` is a flat manually-entered amount, not
computed from a zone table; there's no driver/rider assignment or delivery-status
tracking beyond the Order's own status), and printed KOT/BOT tickets (a real kitchen
would print these — the Phase 6 `IReceiptRenderer` pattern could extend to tickets,
but hasn't yet). No frontend UI was added for transfer/merge/split/standalone-order/
ticket-cancel — real, tested APIs without a screen, same tradeoff as Phase 5's
gap-fill.

**Within Phase 8, still remaining after the gap-fill:** the cash-drawer-open action is
a real, permission-gated, audited endpoint, but still has no actual hardware to
trigger (that's Phase 12's hardware abstraction). The PurchaseOrder-style simple
counter used for invoice numbers is still not a concurrency-safe sequence generator.
No frontend UI was added for the new branch cash-settings screen, day-end-report
history list, or a "open drawer" button — real, tested APIs without a screen, same
tradeoff as prior gap-fills.

**Within Phase 9, still remaining after the gap-fill:** points expiry is a
customer-balance-level approximation, not true per-batch FIFO consumption tracking
(documented above). Void/Refund still don't reverse the loyalty points a sale earned
or redeemed — voiding a points-paid sale does not credit the points back, and
refunding a sale that earned points does not claw them back; this was already true
before the gap-fill and remains a real gap. A coupon still can't be scoped to specific
products/categories or stacked with a Promotion in any smarter way than "apply once,
flatly, to the final total" — no per-line coupon logic. Promotion matching still does
one DB query per sale line (fine at SMB cart sizes, a documented N+1-shaped
inefficiency at large cart sizes). No frontend UI was added for pay-with-points,
coupon management, or loyalty-settings — real, tested APIs without a screen, same
tradeoff as prior gap-fills.

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
Phase 5, 6, 7, 8, and 9 gap-fills are done. The user has asked for Phase 10 gap-fill
next: trend/branch-comparison charts, CSV/PDF export, kitchen/waiter-performance
reports (aggregating existing PreparationTicket data), separate discount/tax report
views, dead/slow-moving stock analysis. After that, Phase 11 (Offline +
Synchronization) is the next new phase.
