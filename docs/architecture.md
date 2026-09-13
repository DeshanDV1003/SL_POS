# Universal POS — Phase 1: Solution Architecture

Status: DRAFT for checkpoint approval
Owner: Solution Architect (virtual role)
Depends on: Phase 0 discovery (see chat record — not yet copied into docs/)

## 1. Goals

- One codebase, one deployable product, usable by a restaurant, a supermarket, a retail
  shop, or a wholesaler — differentiated by **configuration**, not forks.
- "Configuration" = business type flags on the Company/Branch, a permission system on
  Users, and feature modules that can be enabled/disabled per branch (e.g. Table
  Management only matters if BusinessType includes Restaurant).
- Real transactional integrity, real audit trail, real offline tolerance.

## 2. Solution Structure

```
UniversalPOS.sln
src/
  Backend/
    UniversalPOS.Domain/          # Entities, value objects, domain enums, domain events. No EF, no framework deps.
    UniversalPOS.Application/     # Use-case services, DTOs, interfaces (IStockService, IPaymentProvider...),
                                   # FluentValidation validators, AutoMapper/manual mapping profiles.
    UniversalPOS.Infrastructure/  # EF Core DbContext, migrations, repository implementations,
                                   # hardware adapter stubs, payment provider implementations (sandbox + real).
    UniversalPOS.Api/             # ASP.NET Core Web API host: controllers, middleware, auth wiring, Swagger, DI composition root.
  Frontend/
    pos-web/                      # React + TypeScript + Vite. PWA-capable for offline POS terminal use.
tests/
  UniversalPOS.Domain.Tests/      # Pure unit tests: tax/discount/rounding/payment-allocation calculators.
  UniversalPOS.Application.Tests/ # Service-level unit tests with mocked infrastructure.
  UniversalPOS.IntegrationTests/  # Real LocalDB/SQL Server + WebApplicationFactory, full API+DB round trips.
  UniversalPOS.E2E/               # Playwright, golden-path scenarios (restaurant cycle, retail cycle).
docs/
  architecture.md                 # this file
  database-design.md              # Phase 2 ERD/schema
  api-conventions.md              # (added when API work starts)
  project-state.md                # living status doc (§59 of brief) — created at end of each phase
```

Rationale for a **modular monolith** over microservices: a single SMB/chain deployment
does not need independent scaling or deployment of "Inventory" vs "Sales" — it needs low
operational complexity, one database transaction spanning sale+stock+payment, and a
straightforward IIS/Docker deploy story. Module boundaries (Domain namespaces per
module: `Domain.Catalog`, `Domain.Inventory`, `Domain.Sales`, `Domain.Restaurant`,
`Domain.Identity`, `Domain.Purchasing`, `Domain.Crm`) are enforced by project-internal
convention and reviewed at code-review time; a module could be extracted to its own
service later without a rewrite because Application-layer interfaces already isolate it.

## 3. Multi-tenancy / Hierarchy

`Tenant (nullable, reserved for future hosted SaaS) → Company → Branch → Terminal → User`

- v1 ships as **single-tenant-per-deployment** (one Company per install is the common
  case), but every table carries `CompanyId` and, where relevant, `BranchId` from day
  one, enforced via EF Core global query filters. This means the exact same schema
  supports a future multi-tenant hosted product without migration surgery — we just
  stop assuming one Company per database.
- A **Business Type** is a set of flags on `Company`/`Branch` (Restaurant, Retail,
  Grocery, Wholesale, ...) — non-exclusive, since a chain can have a supermarket branch
  and a restaurant branch under one Company. Business type flags gate which modules
  (Table Management, KOT/BOT, Composite/Recipe products) are surfaced to that branch's
  UI and enforced server-side (not just hidden client-side).

## 4. Identity, Roles & Permissions

- Users belong to a Company, are assigned to one or more Branches, and hold Roles.
- Roles are **data**, not code — a Role is a named bundle of Permissions, editable per
  Company (default roles like Cashier/Manager/KitchenStaff/Admin are seeded but not
  hardcoded into logic).
- Permissions are granular action-level strings (`sales.void`, `sales.discount.apply`,
  `sales.price.override`, `cash.drawer.open`, `reports.view.financial`,
  `inventory.adjust`, ...) checked via ASP.NET Core policy-based authorization, resolved
  at runtime from the user's role-permission set — never a hardcoded `if (role ==
  "Manager")` in business logic.
- Sensitive actions (void, refund, price override, discount above threshold) support an
  optional **manager-approval step**: the initiating user's request is held pending a
  second authorized user's PIN/credential confirmation, both users' identities are
  recorded on the audit entry.

## 5. Authentication

- JWT access token (short-lived, ~15 min) + rotating refresh token (opaque, stored
  server-side hashed, single-use — reuse detection revokes the whole chain).
- POS terminals authenticate a *terminal* session (device) plus a *cashier* login (PIN
  or password) — two-factor in practice: a stolen terminal alone isn't a working
  cashier session.
- Passwords hashed with ASP.NET Core Identity's PBKDF2 (or BCrypt) — never reversible,
  never logged.

## 6. API Design

- REST, versioned (`/api/v1/...`), controllers thin — all logic in Application-layer
  services.
- Every endpoint: FluentValidation on the request DTO, policy-based `[Authorize]`,
  consistent response envelope `{ data, error, meta { page, pageSize, total } }`,
  correlation ID on every response header + log line for traceability.
- Centralized exception-handling middleware maps domain exceptions
  (`InsufficientStockException`, `UnauthorizedDiscountException`, ...) to correct HTTP
  status codes with a safe, user-facing message — stack traces never reach the client.

## 7. Transactional Integrity (critical path: the Sale)

A sale is one EF Core transaction spanning:
validate products/prices/stock → compute totals (tax/discount/rounding, see §8) →
insert SaleHeader + SaleLines → insert PaymentTransaction(s) → insert StockLedger
movements (negative) → insert TaxLedger entries → insert AuditLog entry → commit.
Any failure rolls back the entire transaction — a sale is never partially recorded.
Optimistic concurrency (`rowversion` column) on `Product` stock-relevant fields and
`Order` (restaurant) rows protects against two terminals/waiters racing on the same
row; a conflict returns a typed error the client re-fetches and retries against, it is
never silently overwritten.

## 8. Money & Calculation Rules

- All monetary values: `decimal(18,2)` in the database, `decimal` in C# — floating point
  is never used for money.
- Tax, discount, rounding, and payment-allocation logic lives in single, independently
  unit-tested calculator classes in `Domain` (e.g. `TaxCalculator`, `DiscountEngine`,
  `PaymentAllocator`) — no duplicated inline math in controllers/services.
- Rounding rule: round at the line level then sum, using banker's rounding avoided in
  favor of standard "round half away from zero" to match customer-facing cash rounding
  expectations — **cash rounding to the nearest LKR 1 (or configured denomination) is a
  configurable rule per Company**, since LKR coin availability affects real cash
  transactions; this is flagged as a business-configurable rule rather than hardcoded.

## 9. Tax Configuration

Tax is **fully admin-configurable**: `TaxRate` entities (name, percentage,
inclusive/exclusive, effective-from date) assignable per Product/Category, with support
for multiple simultaneous tax components (e.g. VAT + a service charge treated as a
tax-like line) and historical rate changes preserved for audit/reporting on past
invoices, since Sri Lanka has changed VAT twice and abolished SVAT within the last two
years (see research findings below) — a hardcoded rate would break within months of
launch.

**Confirmed via research (2026-09), sources in `docs/research-sri-lanka-pos.md`):**
- Standard VAT is **18%** (since 1 Jan 2024) — seeded as the default `TaxRate`, not
  built into logic. A 43-item VAT exemption schedule exists (medicine, education, some
  essential foods) — modeled as `Product`/`Category` rows simply having no `TaxRate`
  assigned or a 0% exempt rate, never a special-cased code path.
- The **Special Commodity Levy (SCL)** is a distinct levy from VAT on certain imported
  essentials — modeled as an independent `TaxRate`/component type, not folded into VAT,
  so a product can carry SCL instead of or alongside VAT per configuration.
- **SVAT (Simplified VAT) was abolished 1 Oct 2025.** Not implemented as a live feature;
  a config flag may retain read-only historical reporting support if a migrating
  customer needs it, but no new SVAT logic is built.
- A **new mandatory VAT tax invoice format** takes effect **1 July 2026** (IRD Gazette
  2481/22): requires a "TAX INVOICE" heading, supplier 9-digit TIN + registered name/
  address, purchaser TIN/name/address when VAT-registered, new sequential numbering,
  LKR values with no cents, total in words, payment method, and 5-year retention.
  → The invoice/receipt template engine (§ not yet detailed, added in Phase 4) must
  support a **"full tax invoice" mode** (all mandated fields, purchaser TIN capture)
  distinct from a simplified retail receipt, selectable per sale based on whether the
  customer is a VAT-registered purchaser requesting one (a legal right within 14 days
  of purchase per the gazette). Exact field-by-field layout will be verified against
  the primary gazette PDF before the invoice template is finalized in Phase 4/6 — the
  research summary is a secondary-source paraphrase, not a substitute for reading the
  gazette directly.
- Invoice numbering must be **strictly sequential and gapless per Branch** — already
  the plan (§ Database Design `SaleHeader.InvoiceNumber`), reinforced by this
  requirement rather than newly introduced by it.
- **Sri Lanka is actively deploying a National e-Invoicing System** (IRD Notice, May
  2026): real-time transmission of VAT invoice data to **RAMIS via a Web API**, rolling
  out in phases (Phase 1: export-oriented enterprises; Phase 2: all VAT-registered
  persons). A separate government statement describes explicit plans to link **retail
  POS machines directly to IRD** for real-time B2C reporting (a "fiscal register"
  model). Full POS-specific timeline/API spec is unverified, but this is a live 2026
  government program, not speculative.
  → **Architectural commitment made now:** an `IFiscalReportingProvider` interface
  (Application layer) with a queue-based, retry-safe, offline-tolerant transmission
  pipeline (mirrors the offline sync design in §10) is scaffolded in Phase 3/8, with a
  `NullFiscalReportingProvider` (no-op, logs intent) as the default implementation
  until a real RAMIS Web API integration is contracted. Every finalized `SaleHeader`
  enqueues a fiscal-reporting job regardless of whether a real provider is wired up, so
  turning on real reporting later requires no sale-path changes — only swapping the
  provider implementation.
- **Service charge** has no found Sri Lankan statute (unlike Philippines/Maldives,
  which do regulate it) — treated purely as a configurable, itemized, non-tax line
  (`ServiceChargeTotal` on `SaleHeader`, § Database Design), with a per-Branch rate,
  applicability by order type (dine-in vs. takeaway), and a config flag for whether VAT
  applies on top of it (unverified, left to the business/accountant to set, not
  assumed).
- **Price display (tax-inclusive vs. exclusive)** has no confirmed statutory mandate
  found — kept as a per-Category/per-Product configuration rather than a fixed
  convention.
- **VAT registration threshold drops to LKR 36M turnover from 1 Apr 2026** — relevant
  to onboarding/reporting UX (a growing business may cross into VAT-registered status
  mid-operation) but not a schema change; `Company.IsVatRegistered` +
  `VatRegisteredFromDate` already covers it.

## 9a. Payment Gateway Landscape (informs §12 provider abstraction)

- **LankaQR** — national interoperable EMV QR standard on LankaPay/CEFTS rails,
  bank-agnostic.
- **PayHere** — leading local, central-bank-approved gateway aggregating card networks
  (Visa/Mastercard/Amex/Diners) plus local wallets (Genie, FriMi, eZ Cash, mCash,
  Vishwa) behind one integration.
→ The `IPaymentProvider` abstraction (§12) is designed so a single aggregator
integration (PayHere-shaped: one API surface, multiple underlying instruments) is a
normal case, not an exception requiring per-wallet adapters. A `PayHereSandboxProvider`
(or equivalent test-mode adapter) is the natural "local" example sandbox alongside a
generic international card sandbox, once real merchant credentials are available —
until then both remain sandbox-only, never simulating a real captured payment.

## 10. Offline Architecture

- **Online-required always:** card/digital payment capture, initial terminal
  login/token issuance, price/catalog sync, cross-branch stock transfer approval.
- **Offline-capable:** cash sales, KOT/BOT creation and status updates (kitchen network
  is typically LAN-local even if WAN is down), viewing already-synced catalog/customer
  data, held-bill create/recall.
- Local queue: IndexedDB on the terminal stores unsynced sales with a client-generated
  GUID idempotency key and a local sequence number distinct from the server's official
  invoice sequence (server assigns the legal sequential invoice number only on
  successful sync, preventing gaps/duplicates in the legal numbering series).
- Sync: background retry with exponential backoff; server validates stock at sync time
  — if oversold while offline, the sale still posts (a business can't refuse a
  completed cash sale) but raises a `StockReconciliationFlag` for manager review rather
  than silently going negative unnoticed.

## 11. Hardware Abstraction

- `IReceiptPrinter`, `IKitchenPrinter`, `ICashDrawer`, `IBarcodeScanner`,
  `ICustomerDisplay`, `IScale` interfaces defined in Application layer.
- Concrete implementations live in the frontend/local-agent boundary (ESC/POS over
  WebUSB/serial or a small local print-agent service) — the backend never talks to
  hardware directly; it only produces the data (formatted receipt payload, KOT payload)
  the frontend hands to the adapter.

## 12. Payment Provider Abstraction

`IPaymentProvider` interface (`AuthorizeAsync`, `CaptureAsync`, `RefundAsync`) with:
- `CashPaymentProvider` — real, no external dependency.
- `SandboxCardPaymentProvider` — deterministic test provider (approves/declines based
  on test card numbers) used until a real gateway contract/credentials exist.
- Real gateway implementations added later behind the same interface — sale/payment
  domain logic never changes when a real processor is plugged in.
- The system never marks a non-cash payment as "completed" without a provider
  response confirming it; a declined/failed provider response fails the payment step
  and the sale transaction rolls back to "awaiting payment," never to "paid."

## 13. Reporting

Read-heavy reporting queries run against the same SQL Server database initially
(indexed appropriately for the known report shapes); a read-replica or reporting
schema is a documented future scaling step, not built in v1 given SMB-scale data
volumes.

## 14. Deployment

- Dev: LocalDB + `dotnet run` + Vite dev server.
- On-prem branch server: IIS (ASP.NET Core Module) + SQL Server Express/Standard,
  documented in a deployment guide (Phase 15).
- Cloud/staging: Docker Compose (API container + SQL Server container or Azure SQL).
- Config via `appsettings.{Environment}.json` + environment variables for secrets;
  connection strings and JWT signing keys never committed to source control.

## 15. Open Items Pending Research Task

- Sri Lankan VAT rate(s) and tax-invoice content rules → seeds `TaxRate` defaults.
- IRD e-invoicing/fiscal device mandate status → determines whether an
  `IFiscalReportingProvider` stub is needed in v1 or can be deferred entirely.
- Local payment gateway/wallet landscape (LankaQR etc.) → informs which sandbox
  provider name to seed as the "local" example alongside a generic card sandbox.

This document will be updated once the research agent reports back; nothing above is
blocked by it because every genuinely local-specific value is already routed through
configuration/seed data rather than hardcoded logic.
