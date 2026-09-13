# Universal POS — Project State

Last updated: 2026-09-13, after Phase 4.

## Current Phase
Phase 4 (Master Data) complete and verified end-to-end. Awaiting go-ahead for Phase 5
(Inventory + Purchasing: stock ledger, transfers, adjustments, PO -> GRN -> invoice).

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
  14 integration tests + 2 unit tests, all passing.

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
Everything outside Foundation + Master Data: Inventory (stock ledger, transfers,
adjustments — Product exists but has no stock quantity or movement history yet),
Purchasing workflow (PO -> GRN -> invoice; Supplier exists but no purchase documents),
Retail POS, Restaurant POS/KOT/BOT/KDS, Payments, Cash management, CRM/Loyalty beyond
the Customer record itself, Promotions, Reporting, Offline/sync, Hardware abstraction
implementations, real fiscal/e-invoice provider, real payment gateway integration.
AuditLog/ApprovalRequest tables exist but nothing writes to them yet — the first
sensitive action built (e.g. a discount or void in Phase 6) must wire up real audit
writes, not treat the table as decorative. ProductComponent/ProductModifierGroup
tables exist but nothing reads them yet (no checkout logic exists to expand a bundle
or apply a modifier's price adjustment).

## Architecture Decisions Locked In (see docs/architecture.md for full rationale)
- Modular monolith, not microservices.
- React (not Blazor/Angular) for the POS terminal frontend, for offline/PWA maturity.
- Tax, service charge, and invoice numbering are admin-configurable, never hardcoded —
  reinforced by Sri Lanka's real regulatory churn (VAT rate changes, SVAT abolition,
  new mandatory invoice format, incoming IRD e-invoicing).
- `IFiscalReportingProvider` and `FiscalTransmission` exist now, ahead of most other
  business logic, because Sri Lanka's e-invoicing rollout is an active 2026 program.

## Next Task
Phase 5 — Inventory + Purchasing: StockLedger (immutable movement log), StockOnHand
summary, ProductBatch, StockTransfer, StockAdjustment, StockCount, and the
PurchaseOrder -> GoodsReceivedNote -> PurchaseInvoice -> SupplierPayment workflow,
following the same real-schema-then-migration-then-API-then-tests discipline as
Phases 3-4.
