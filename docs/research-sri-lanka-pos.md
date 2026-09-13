# Research: Sri Lankan Tax/Invoicing Requirements & POS Market Landscape

Compiled 2026-09-13 by research agent (WebSearch/WebFetch). Secondary-source
synthesis — verify field-level specifics (especially the mandatory invoice format)
against primary IRD gazette text before finalizing templates. Items marked
UNVERIFIED could not be confirmed via search and must not be treated as fact.

## 1. Tax & Invoicing

- **Standard VAT: 18%**, effective since 1 Jan 2024 ([IRD](https://www.ird.gov.lk/en/type%20of%20taxes/sitepages/value%20added%20tax%20(vat).aspx), [TaxWise](https://taxwise.lk/calculators/vat)).
- **43-item VAT exemption schedule** (medicines, education, some essential
  foods/grains, infant formula, books, fuel, passenger transport; some milk/yogurt
  added 2025) ([EconomyNext](https://economynext.com/sri-lanka-says-43-items-including-medicine-foods-education-exempt-from-vat-145630/), [IRD schedule PDF](https://www.ird.gov.lk/en/Lists/Latest%20News%20%20Notices/Attachments/92/Schedule%20of%20Goods%20or%20Services%20Exempted%20from%20VAT.pdf)).
- **Special Commodity Levy (SCL)**: separate levy on certain imported essentials.
  UNVERIFIED exact interaction with VAT per item.
- **VAT mandatory registration threshold drops to LKR 36M** turnover from 1 Apr 2026
  (from LKR 60M) ([Bestead](https://bpc.lk/whats-changing-under-vat-sscl-from-1-april-2026/)).
- **New mandatory standardized VAT tax invoice format**: IRD Gazette Extraordinary No.
  2481/22 (27 Mar 2026), fully effective **1 Jul 2026** ([Daily FT](https://www.ft.lk/columns/New-VAT-tax-invoice-format-mandatory-from-1-April-2026/4-790220), [VATupdate](https://www.vatupdate.com/2026/03/30/sri-lanka-mandates-new-standardized-vat-tax-invoice-format-effective-april-1-2026/), [gazette PDF](https://www.ird.gov.lk/en/publications/Gazette_Documents/2026_2481-22_E.pdf)).
  Reported required elements (verify against gazette directly): "TAX INVOICE" heading;
  supplier 9-digit TIN + registered name/address matching VAT certificate; purchaser
  TIN/name/address when VAT-registered; new sequential numbering; invoice + delivery
  dates; VAT rate; LKR values with no cents; total in words; payment method; 5-year
  retention; 14-day customer right to request a proper tax invoice post-purchase.
- **National e-Invoicing System actively rolling out** under the 2026 budget: real-time
  VAT invoice transmission via Web API to RAMIS (IRD, 4 May 2026 notice). Phase 1:
  export-oriented VAT-registered enterprises (tea auctions integrated as of May 2026).
  Phase 2: all VAT-registered persons ([VATupdate](https://www.vatupdate.com/2026/05/09/sri-lanka-launches-national-e-invoicing-system-for-vat-under-2026-budget-full-rollout-by-year-end/), [EDICOM](https://edicomgroup.com/blog/electronic-invoice-sri-lanka-evat), [regfollower](https://regfollower.com/sri-lanka-ird-begins-national-e-invoicing-rollout-under-2026-budget/)).
  Separately, EconomyNext reports government plans to **link retail POS machines
  directly to IRD** for B2C real-time reporting, "fiscal register" style
  ([EconomyNext](https://economynext.com/sri-lanka-plans-e-invoicing-for-vat-link-pos-machines-to-inland-revenue-228177/)). POS-specific timeline/API spec: UNVERIFIED beyond "targeted by
  year-end 2026" in one source.
- **SVAT abolished 1 Oct 2025**, replaced by a risk-based VAT refund system (refunds
  targeted within 45 days by risk category) ([VATupdate](https://www.vatupdate.com/2025/09/07/sri-lanka-abolishes-svat-reinstates-traditional-vat-with-new-risk-based-refund-system/), [IRD FAQ PDF](https://www.ird.gov.lk/en/Lists/Latest%20News%20%20Notices/Attachments/702/VAT31072025_SVAT_Repeal_FAQs_QG.pdf)).
- **Service charge**: no Sri Lankan statute found (unlike Philippines RA 11360 or
  Maldives' Employment Act amendment). Described as standard trade practice, disclosed
  on menus, not compulsory if not disclosed; Dept. of Labour reportedly has no specific
  tipping/service-charge legislation ([Sunday Times](https://www.sundaytimes.lk/240602/news/hospitality-trade-where-tipping-scales-tip-in-favour-of-managers-558988.html)). UNVERIFIED
  whether Shop & Office Employees Act or hotel wage-board ordinances touch this.
- **Price display**: Consumer Affairs Authority Act No. 9 of 2003 empowers CAA on
  price-marking/labelling and price caps by gazette ([CAA Act PDF](https://caa.gov.lk/web/images/Act/CAA_Act_E.pdf)), but no explicit
  tax-inclusive-vs-exclusive display mandate found. UNVERIFIED.
- Note: "NBT" (Nation Building Tax) still appears in some vendor marketing copy but
  was abolished years ago federally — treat as stale marketing language, not current
  law.

## 2. POS Market Landscape

- Vendors serving/marketed in Sri Lanka: international (Toast, Lavu, Square, HDPOS
  Smart) and local (**FusionRetail, SARIS POS, Applantics, POSLK, TILLMAX, SpicePOS,
  StoreMate POS** by Parallax Technologies, possystem.lk, myPOS.lk, RetailIT.lk)
  ([ensun](https://ensun.io/search/point-of-sale-pos/sri-lanka), [SoftwareSuggest](https://www.softwaresuggest.com/point-of-sale-pos-software/srilanka), [Applantics](https://applantics.com/pos-system-sri-lanka), [SARIS](https://sarislabs.com/pos)).
- Commonly advertised: cloud+offline hybrid, multi-branch, inventory/barcode, loyalty/
  CRM, Sinhala/Tamil UI, local bank integrations. Explicit "KOT/BOT" terminology not
  directly confirmed in sources reviewed — UNVERIFIED as a locally marketed term,
  though functionally implied by restaurant modules.
- Pain points/gaps: no user complaint threads or forum discussions surfaced — only
  vendor marketing. UNVERIFIED; would need a targeted survey (SME Facebook groups,
  Reddit) for real evidence.
- Payment landscape: **LankaQR** (national interoperable EMV QR, LankaPay/CEFTS rails,
  bank-agnostic); **PayHere** (leading local, central-bank-approved gateway,
  aggregates card networks + local wallets Genie/FriMi/eZ Cash/mCash/Vishwa)
  ([LankaPay](https://www.lankapay.net/en/for-financial/lanka-qr), [PayHere](https://www.payhere.lk/), [inai.io](https://inai.io/blog/top-8-payment-gateways-in-sri-lanka)). Other gateways
  referenced in comparison articles but not individually confirmed — UNVERIFIED list
  completeness.

## Architectural Consequences (already applied in `architecture.md` / `database-design.md`)

1. Tax config (rate, exemptions, SCL, invoice numbering) is admin-configurable, never
   hardcoded — reinforced, not newly caused, by this research.
2. A "full tax invoice" mode (mandated fields, purchaser TIN) is required alongside a
   simplified receipt mode, selectable per sale.
3. An `IFiscalReportingProvider` interface with a queue-based, offline-tolerant
   transmission pipeline is scaffolded from Phase 3/8 onward, defaulting to a no-op
   implementation until a real RAMIS integration is contracted — this is treated as a
   near-term real requirement, not a hypothetical.
4. Service charge and tax-inclusive/exclusive display remain fully configurable per
   Branch/Category rather than assumed.
5. Payment provider abstraction treats a single aggregator (PayHere-shaped) as the
   normal integration pattern for reaching multiple local wallets.
