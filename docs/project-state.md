# Universal POS — Project State

Last updated: 2026-09-14, after Phase 12 (Hardware Abstraction, software-only pieces).

## Current Phase
Phases 0–11 are complete (Phase 11 across both its backend and frontend layers — see
Completed Modules). Phase 12 (Hardware Abstraction) is now done for the pieces that
can be built as real, verifiable software with no physical hardware attached: the
user explicitly chose that scope over stubbing out untestable printer/drawer/scale
I/O, given six device types split cleanly into "buildable and testable today" vs.
"needs real hardware to mean anything." A KOT/BOT ticket text renderer (backend,
curl/integration-tested) and a customer-facing display (frontend, a second browser
window synced via BroadcastChannel) are built. Printer/cash-drawer/scale hardware
I/O (WebUSB/serial ESC-POS, a USB drawer kick) remains explicitly unbuilt — see the
Phase 12 entry below for why.

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

- **Phase 10 gap-fill — trend/branch-comparison charts, CSV export, kitchen/waiter
  performance, discount/tax reports, dead-stock analysis**: `GET .../sales-trend`
  returns daily net-sales/transaction-count buckets — the series behind a revenue
  chart — verified against a real sale's own bucket. `GET /reports/branch-comparison`
  runs the same summary side by side for every branch in the caller's company.
  `discount-report` and `tax-report` split real numbers that already existed inside
  `sales-summary` into their own views: discount total by line vs. coupon, top
  discounted products, and tax collected grouped by rate — verified a coupon and a
  manual line discount both show up correctly, and total tax equals the sum of its
  own by-rate breakdown. `kitchen-performance` and `waiter-performance` aggregate
  real `PreparationTicket`/`Order` data (unused since Phase 7) into ticket counts,
  cancellations, and average prep time per kitchen station, and order counts/billed
  revenue per waiter — verified end-to-end through a real send-to-kitchen →
  mark-Ready flow, not synthetic data. `slow-moving-stock` flags any product with
  stock on hand whose last completed sale (if any) is older than a caller-supplied
  `staleAfterDays` (default 30) — verified against a freshly stocked, never-sold
  product. Every list-shaped report accepts `?format=csv` (a small dependency-free
  `ReportCsvWriter` that reflects over the DTO's public properties) and returns a
  downloadable `text/csv` file — verified content-type and header row. PDF export
  was **not** built: no PDF library exists in this codebase yet, and adding one is a
  dependency decision better made explicitly with the user (the same reasoning that
  kept Phase 6 gap-fill's receipt as plain text rather than a PDF) — CSV covers the
  "get this out of the system" need without that decision. 88 integration tests + 17
  unit tests, all passing.

- **Phase 11 — Offline + Synchronization (backend layer)**: `CreateSaleRequest.IsOfflineSync`
  marks a sale as arriving from a terminal's offline queue rather than being taken
  live; it requires `ClientIdempotencyKey` (validated — a sync retry must never
  double-book a sale, verified 400 without one) and records `ClientCreatedAtUtc`
  (when it actually happened) distinct from `CompletedAtUtc` (when the server
  sequenced it). Negative stock was already never blocked anywhere in this codebase
  (`StockService.PostMovementAsync`, since Phase 5) — what Phase 11 adds is a real
  `StockReconciliationFlag` raised specifically when an offline-synced sale drives
  stock negative, for manager review (`GET`/`POST .../resolve` under
  `inventory.reconciliation.resolve`) — verified an offline oversell raises exactly
  one flag with the correct shortfall quantity, a same-day online oversell raises
  none (documented distinction), and resolving twice is rejected (409). A terminal
  heartbeat (`POST /branches/{id}/terminals/{id}/heartbeat`) updates
  `Terminal.LastSeenAtUtc`, previously a schema column nothing ever wrote to — any
  authenticated user may call it, since it's just "prove you're online," not a
  privileged action. 95 integration tests + 17 unit tests, all passing.

  One real pre-existing seeding bug found via testing (not introduced by this
  phase, but only surfaced because it added a new permission code): `DbSeeder`'s
  role-permission sync only wrote `RolePermissions` for a *newly created* system
  role — a role that already existed in the database (as every seeded database's
  Admin/Manager/Cashier do, after the first run) never picked up a permission code
  added to its definition later. `inventory.reconciliation.resolve` was invisible
  to `admin.lfm`'s already-issued token on the dev database until this was fixed
  (existing roles now get diffed against their current definition and missing
  permissions added). This means every permission added in Phases 8-11
  (`cash.drawer.open` aside, which predates this) reached brand-new test databases
  correctly but would have silently never reached a real, already-deployed
  database — now fixed for all of them going forward.

- **Phase 11 — Offline + Synchronization (frontend layer)**: picked back up after the
  backend-only pass above. `src/Frontend/pos-web/src/offline/` is a real,
  dependency-free IndexedDB-backed queue (`db.ts`, `offlineSalesQueue.ts`) and a
  singleton background sync manager (`syncManager.ts`) that processes queued sales
  strictly in creation order — so the server's sequential invoice numbering assigns
  numbers in the order sales actually happened, not the order the network happened
  to let them through — halting the whole pass on a network failure but marking
  just one sale `failed` (for manual review) on a real server rejection, so one bad
  item never blocks the rest. `CheckoutPage` (the only screen that charges Cash —
  card/digital stay online-required per §10) queues offline instead of erroring
  whenever `navigator.onLine` is false or the live checkout request throws a
  transport-level failure (`!(err instanceof ApiError)`, since `apiFetch` only ever
  throws `ApiError` for an actual HTTP response); a "Sale Saved Offline" screen and
  an `OfflineStatusBadge` (pending/failed counts, a manual "Sync now") replace a
  hard error. `recordTerminalHeartbeat` is now actually called from `CheckoutPage`
  every 60s while online. `public/sw.js` is a small hand-rolled service worker
  (no workbox/vite-plugin-pwa — one cache, one fetch strategy doesn't need a
  library) that caches the app shell so the page itself can still load offline; it
  deliberately never intercepts `/api/*` traffic, leaving that entirely to the
  IndexedDB queue above, and only registers in production builds (a dev-mode
  service worker would fight Vite's HMR websocket).

  **How this was verified, honestly:** `tsc -b` and `vite build` both pass cleanly,
  `oxlint` shows no new warnings from any of this code, and the dev server serves
  the updated bundle. What was **not** verified is true browser offline behavior —
  DevTools network throttling, watching IndexedDB fill and drain, confirming the
  service worker actually serves a cached shell with the network off — because
  driving a real browser isn't something this session can do. That verification is
  the user's to run: open the app, DevTools → Network → Offline, complete a cash
  sale (expect the "Sale Saved Offline" screen), go back online, and confirm the
  sale appears server-side with a real invoice number.

- **Phase 12 — Hardware Abstraction (software-only pieces)**: scoped explicitly with
  the user — of the six device types in docs/architecture.md §11 (receipt printer,
  kitchen printer, cash drawer, barcode scanner, customer display, scale), only the
  ones buildable and verifiable as real software without physical hardware were
  built this pass.
  - **KOT/BOT ticket renderer** (`IKotTicketRenderer`/`KotTicketRenderer`, backend):
    mirrors the Phase 6 `IReceiptRenderer` pattern exactly, rendering a
    `PreparationTicket` as plain text sized for a narrower (58mm/32-char) kitchen
    printer — station name and "KITCHEN ORDER TICKET" vs. "BAR ORDER TICKET"
    (by `KitchenStation.Category`), table name for a dine-in order or order
    type/phone for a standalone Takeaway/Delivery order, each line's quantity/name/
    notes. Exposed at `GET /branches/{id}/tickets/{id}/print`, no special
    permission (any authenticated staff member can view a ticket, same as a
    receipt). This fills the gap explicitly left open at the end of Phase 7's
    gap-fill ("printed KOT/BOT tickets... hasn't yet"). Verified end-to-end via
    curl for all three cases (dine-in/kitchen, dine-in/bar, standalone takeaway)
    plus a 404 for an unknown ticket; 4 new integration tests (99 total + 17 unit,
    all passing).
  - **Customer-facing display** (`ICustomerDisplay`, frontend): a genuinely
    hardware-free implementation — a customer-facing monitor just runs a second
    browser window of the same origin (`window.open('/customer-display', ...)`
    from a new "Open Customer Display" button on `CheckoutPage`), and the two
    windows talk over `BroadcastChannel` (a standard, no-dependency browser API),
    with no server round-trip. Shows the live cart (product/qty/line total,
    running total) while shopping, then a "Thank you" + grand total/change-due
    screen after checkout. One real bug caught and fixed before it shipped: the
    existing cart-changed broadcast effect would immediately overwrite the
    "Thank you" message with an empty "shopping" screen the instant `setCart([])`
    ran after checkout — fixed with a `customerDisplayShowingReceipt` flag that
    suppresses that effect until the cashier clicks New Sale.
  - **Barcode scanner**: confirmed already working, no new code needed — the
    existing scan-to-search input on `CheckoutPage` already accepts any
    keyboard-wedge barcode scanner (the overwhelming majority of retail/restaurant
    scanners), since those emulate a keyboard and just "type" the barcode followed
    by Enter.
  - **Explicitly not built, and why:** ESC/POS printing over WebUSB/serial for a
    real receipt/kitchen printer, a USB cash-drawer kick command, and any scale
    integration. All three need actual hardware plugged in for the code to mean
    anything — without it, "implementing" them would be unverified guesswork
    dressed up as done, which this project's own discipline (verify every phase
    end-to-end before calling it finished) rules out. The existing `window.print()`
    receipt/ticket flows and the audited-but-hardware-free
    `POST /cash-drawer/open` endpoint (Phase 8) remain the real, honest state of
    those two until real devices are available to build and test against.

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

**Within Phase 10, still remaining after the gap-fill:** PDF export (CSV only — see
above; adding a PDF library is a dependency decision for the user to make, not one to
default into). `slow-moving-stock`'s velocity signal is "days since last sale", not a
true sales-velocity trend over multiple periods — a product that sold once heavily
long ago and nothing since looks identical to one that never sold at all, beyond the
single `LastSoldAtUtc` timestamp. Kitchen/waiter performance reports have no frontend
UI yet — real, tested APIs without a screen, same tradeoff as every prior gap-fill.
Branch-comparison has no chart UI either — it is a real, tested JSON endpoint a chart
would consume, not a rendered chart.

**Within Phase 11, still remaining now that both layers are built:** true browser
verification of the offline queue was not performed by this session (see the
Completed Modules entry above for exactly what was and wasn't verified, and the
steps to do it) — that's the single biggest open item. `StockReconciliationFlag`
still has no frontend screen (real, tested API without one, same tradeoff as every
other gap-fill). Background Sync API (letting the browser retry even when the tab
isn't focused) was not used — the sync manager instead relies on the page being
open plus an `online` event listener and a 30s timer, which covers the realistic
"cashier's tab stays open all shift" case but not "closed the tab while offline."
The offline queue only covers Cash retail checkout (`CheckoutPage`) — KOT/BOT
creation/status updates, held-bill create/recall, and restaurant billing are all
listed as offline-capable in docs/architecture.md §10 but weren't wired into this
queue. Cross-branch stock transfer approval and catalog/price sync remain
online-required by design (§10), not offline-capable gaps.

**Within Phase 12, explicitly deferred until real hardware is available:** ESC/POS
printing over WebUSB/serial (`IReceiptPrinter`/`IKitchenPrinter`), a USB cash-drawer
kick command (`ICashDrawer` — the audited API trigger from Phase 8 exists, the
actual hardware signal doesn't), and any `IScale` integration. Also not verified:
the customer display and the KDS "Print" button (both wired into the UI, both
build/typecheck/lint clean) haven't been exercised in a real browser — same
browser-verification limitation as Phase 11's frontend layer, see that entry.

**Everything after Phase 12:** real fiscal/e-invoice provider, and real payment
gateway integration.

## Architecture Decisions Locked In (see docs/architecture.md for full rationale)
- Modular monolith, not microservices.
- React (not Blazor/Angular) for the POS terminal frontend, for offline/PWA maturity.
- Tax, service charge, and invoice numbering are admin-configurable, never hardcoded —
  reinforced by Sri Lanka's real regulatory churn (VAT rate changes, SVAT abolition,
  new mandatory invoice format, incoming IRD e-invoicing).
- `IFiscalReportingProvider` and `FiscalTransmission` exist now, ahead of most other
  business logic, because Sri Lanka's e-invoicing rollout is an active 2026 program.

## Next Task
Phase 12's software-only pieces are done (KOT/BOT ticket renderer, customer-facing
display, confirmed barcode-scanner compatibility). The user should manually verify
both this and Phase 11's frontend layer in a real browser (see each Completed
Modules entry above for exact steps) before treating either as fully proven.
Remaining hardware-dependent work (real ESC/POS printing, a cash-drawer kick, a
scale) needs physical devices to build against and isn't actionable until then.
Otherwise: extend the Phase 11 offline queue to KOT/BOT and held-bill flows, or
move to the real fiscal/e-invoice provider or payment gateway integration.
