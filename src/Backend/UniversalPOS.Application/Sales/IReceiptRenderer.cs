namespace UniversalPOS.Application.Sales;

/// <summary>
/// Renders a finalized sale as plain text formatted for an 80mm (or narrower)
/// thermal receipt printer — the actual paper-width formatting a hardware print
/// adapter would send verbatim, per docs/architecture.md §11 (business logic never
/// talks to hardware directly, it only produces the payload). InvoiceMode.FullTaxInvoice
/// includes the fields mandated by IRD Gazette 2481/22 (effective 1 Jul 2026) — see
/// docs/research-sri-lanka-pos.md; field-by-field layout should be re-verified against
/// the primary gazette text before this goes to production.
/// </summary>
public interface IReceiptRenderer
{
    Task<string> RenderAsync(long companyId, long saleId, CancellationToken cancellationToken = default);
}
