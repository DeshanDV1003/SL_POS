using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Reporting.Dtos;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Reporting;

/// <summary>
/// Every report here is a real aggregation query over the same SaleHeader/SaleLine/
/// SalePayment/StockOnHand data every other module writes — never a separately
/// tracked shadow total that could drift from reality.
/// </summary>
public class ReportingService : IReportingService
{
    private readonly IApplicationDbContext _db;

    public ReportingService(IApplicationDbContext db)
    {
        _db = db;
    }

    private IQueryable<SaleHeader> CompletedSales(long companyId, long branchId, DateTime fromUtc, DateTime toUtc) =>
        _db.SaleHeaders.Where(s => s.CompanyId == companyId && s.BranchId == branchId && s.Status == SaleStatus.Completed
            && s.CompletedAtUtc >= fromUtc && s.CompletedAtUtc < toUtc);

    public async Task<SalesSummaryDto> GetSalesSummaryAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var sales = await CompletedSales(companyId, branchId, fromUtc, toUtc).ToListAsync(cancellationToken);
        var voidCount = await _db.SaleHeaders.CountAsync(s => s.CompanyId == companyId && s.BranchId == branchId && s.Status == SaleStatus.Voided
            && s.CompletedAtUtc >= fromUtc && s.CompletedAtUtc < toUtc, cancellationToken);

        return new SalesSummaryDto
        {
            GrossSales = sales.Sum(s => s.SubTotal),
            DiscountTotal = sales.Sum(s => s.DiscountTotal),
            TaxTotal = sales.Sum(s => s.TaxTotal),
            ServiceChargeTotal = sales.Sum(s => s.ServiceChargeTotal),
            NetSales = sales.Sum(s => s.GrandTotal),
            TransactionCount = sales.Count,
            VoidCount = voidCount,
            AverageSaleValue = sales.Count > 0 ? Domain.Sales.Money.Round(sales.Sum(s => s.GrandTotal) / sales.Count) : 0m,
        };
    }

    public async Task<IReadOnlyList<SalesByProductDto>> GetSalesByProductAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var saleIds = CompletedSales(companyId, branchId, fromUtc, toUtc).Select(s => s.Id);

        return await _db.SaleLines
            .Where(l => saleIds.Contains(l.SaleHeaderId))
            .Join(_db.Products, l => l.ProductId, p => p.Id, (l, p) => new { l.Quantity, l.LineTotal, p.Id, p.Name })
            .GroupBy(x => new { x.Id, x.Name })
            .Select(g => new SalesByProductDto
            {
                ProductId = g.Key.Id,
                ProductName = g.Key.Name,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal),
            })
            .OrderByDescending(x => x.Revenue)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SalesByCategoryDto>> GetSalesByCategoryAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var saleIds = CompletedSales(companyId, branchId, fromUtc, toUtc).Select(s => s.Id);

        var rows = await _db.SaleLines
            .Where(l => saleIds.Contains(l.SaleHeaderId))
            .Join(_db.Products, l => l.ProductId, p => p.Id, (l, p) => new { l.LineTotal, p.CategoryId })
            .ToListAsync(cancellationToken);

        var categoryIds = rows.Where(r => r.CategoryId.HasValue).Select(r => r.CategoryId!.Value).Distinct().ToList();
        var categoryNames = await _db.Categories.Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return rows
            .GroupBy(r => r.CategoryId)
            .Select(g => new SalesByCategoryDto
            {
                CategoryId = g.Key,
                CategoryName = g.Key.HasValue ? categoryNames.GetValueOrDefault(g.Key.Value, "(unknown)") : "(uncategorized)",
                Revenue = g.Sum(x => x.LineTotal),
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }

    public async Task<IReadOnlyList<SalesByCashierDto>> GetSalesByCashierAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await CompletedSales(companyId, branchId, fromUtc, toUtc)
            .GroupBy(s => s.CashierUserId)
            .Select(g => new { CashierUserId = g.Key, Count = g.Count(), Revenue = g.Sum(s => s.GrandTotal) })
            .ToListAsync(cancellationToken);

        var userIds = rows.Select(r => r.CashierUserId).ToList();
        var userNames = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return rows
            .Select(r => new SalesByCashierDto
            {
                CashierUserId = r.CashierUserId,
                CashierName = userNames.GetValueOrDefault(r.CashierUserId, "(unknown)"),
                TransactionCount = r.Count,
                Revenue = r.Revenue,
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }

    public async Task<IReadOnlyList<SalesByPaymentMethodDto>> GetSalesByPaymentMethodAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var saleIds = CompletedSales(companyId, branchId, fromUtc, toUtc).Select(s => s.Id);

        return await _db.SalePayments
            .Where(p => saleIds.Contains(p.SaleHeaderId))
            .GroupBy(p => p.Method)
            .Select(g => new SalesByPaymentMethodDto { Method = g.Key.ToString(), Total = g.Sum(p => p.Amount) })
            .OrderByDescending(x => x.Total)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockValuationDto>> GetStockValuationAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        return await (
            from soh in _db.StockOnHands
            join p in _db.Products on soh.ProductId equals p.Id
            where soh.CompanyId == companyId && soh.BranchId == branchId
            select new StockValuationDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                QuantityOnHand = soh.QuantityOnHand,
                CostPrice = p.CostPrice,
                ValuationAtCost = soh.QuantityOnHand * p.CostPrice,
            }).OrderByDescending(x => x.ValuationAtCost)
            .ToListAsync(cancellationToken);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var sevenDaysAgo = todayStart.AddDays(-7);

        var todaySummary = await GetSalesSummaryAsync(companyId, branchId, todayStart, todayStart.AddDays(1), cancellationToken);
        var last7DaysSummary = await GetSalesSummaryAsync(companyId, branchId, sevenDaysAgo, todayStart.AddDays(1), cancellationToken);
        var topProducts = (await GetSalesByProductAsync(companyId, branchId, sevenDaysAgo, todayStart.AddDays(1), cancellationToken)).Take(5).ToList();

        var lowStockCount = await (
            from soh in _db.StockOnHands
            join p in _db.Products on soh.ProductId equals p.Id
            where soh.CompanyId == companyId && soh.BranchId == branchId && soh.QuantityOnHand <= p.ReorderLevel
            select soh.Id).CountAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            TodayNetSales = todaySummary.NetSales,
            TodayTransactionCount = todaySummary.TransactionCount,
            Last7DaysNetSales = last7DaysSummary.NetSales,
            LowStockProductCount = lowStockCount,
            TopProducts = topProducts,
        };
    }
}
