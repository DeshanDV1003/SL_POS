using System.Text;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales;

public class ReceiptRenderer : IReceiptRenderer
{
    private const int Width = 42; // standard 80mm thermal printer character width at default font

    private readonly IApplicationDbContext _db;

    public ReceiptRenderer(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> RenderAsync(long companyId, long saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _db.SaleHeaders.Include(s => s.Lines).Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == saleId && s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(SaleHeader), saleId);

        var company = await _db.Companies.FirstAsync(c => c.Id == companyId, cancellationToken);
        var branch = await _db.Branches.FirstAsync(b => b.Id == sale.BranchId, cancellationToken);

        var productIds = sale.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productNames = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var sb = new StringBuilder();

        if (sale.InvoiceMode == InvoiceMode.FullTaxInvoice)
        {
            RenderFullTaxInvoiceHeader(sb, company, branch, sale);
        }
        else
        {
            RenderSimplifiedHeader(sb, company, branch, sale);
        }

        sb.AppendLine(new string('-', Width));
        foreach (var line in sale.Lines)
        {
            var name = productNames.GetValueOrDefault(line.ProductId, "(item)");
            sb.AppendLine(Truncate(name, Width));
            var detail = $"  {line.Quantity:0.###} x {line.UnitPrice:0.00}";
            var total = line.LineTotal.ToString("0.00");
            sb.AppendLine(PadLine(detail, total));
            if (line.LineDiscountAmount > 0)
            {
                sb.AppendLine(PadLine("  Discount", $"-{line.LineDiscountAmount:0.00}"));
            }
        }
        sb.AppendLine(new string('-', Width));

        sb.AppendLine(PadLine("Subtotal", sale.SubTotal.ToString("0.00")));
        if (sale.DiscountTotal > 0) sb.AppendLine(PadLine("Discount", $"-{sale.DiscountTotal:0.00}"));
        if (sale.ServiceChargeTotal > 0) sb.AppendLine(PadLine("Service Charge", sale.ServiceChargeTotal.ToString("0.00")));
        sb.AppendLine(PadLine("VAT/Tax", sale.TaxTotal.ToString("0.00")));
        sb.AppendLine(PadLine("TOTAL", sale.GrandTotal.ToString("0.00")));
        sb.AppendLine();

        foreach (var payment in sale.Payments)
        {
            sb.AppendLine(PadLine(payment.Method.ToString(), payment.Amount.ToString("0.00")));
        }

        sb.AppendLine();
        sb.AppendLine(Center(sale.Status == SaleStatus.Refunded ? "*** CREDIT NOTE ***" : "Thank you!", Width));

        return sb.ToString();
    }

    private static void RenderSimplifiedHeader(StringBuilder sb, Domain.Organization.Company company, Domain.Organization.Branch branch, SaleHeader sale)
    {
        sb.AppendLine(Center(company.Name, Width));
        sb.AppendLine(Center(branch.Name, Width));
        if (!string.IsNullOrWhiteSpace(branch.Address)) sb.AppendLine(Center(branch.Address, Width));
        sb.AppendLine();
        sb.AppendLine($"Invoice: {sale.InvoiceNumber}");
        sb.AppendLine($"Date: {sale.CompletedAtUtc:yyyy-MM-dd HH:mm}");
    }

    private static void RenderFullTaxInvoiceHeader(StringBuilder sb, Domain.Organization.Company company, Domain.Organization.Branch branch, SaleHeader sale)
    {
        // Fields per IRD Gazette 2481/22 (effective 1 Jul 2026) as summarized in
        // docs/research-sri-lanka-pos.md — re-verify against the primary gazette text.
        sb.AppendLine(Center("TAX INVOICE", Width));
        sb.AppendLine(Center(company.LegalName, Width));
        sb.AppendLine(Center(branch.Name, Width));
        if (!string.IsNullOrWhiteSpace(branch.Address)) sb.AppendLine(Center(branch.Address, Width));
        sb.AppendLine($"Supplier TIN: {company.TaxRegistrationNo}");
        sb.AppendLine();
        sb.AppendLine($"Invoice No: {sale.InvoiceNumber}");
        sb.AppendLine($"Invoice Date: {sale.CompletedAtUtc:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("Purchaser:");
        sb.AppendLine($"  Name: {sale.PurchaserName}");
        sb.AppendLine($"  TIN: {sale.PurchaserTin}");
        if (!string.IsNullOrWhiteSpace(sale.PurchaserAddress)) sb.AppendLine($"  Address: {sale.PurchaserAddress}");
    }

    private static string PadLine(string left, string right)
    {
        var space = Width - left.Length - right.Length;
        return space > 0 ? left + new string(' ', space) + right : left[..Math.Min(left.Length, Width - right.Length)] + right;
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        var padding = (width - text.Length) / 2;
        return new string(' ', padding) + text;
    }

    private static string Truncate(string text, int width) => text.Length > width ? text[..width] : text;
}
