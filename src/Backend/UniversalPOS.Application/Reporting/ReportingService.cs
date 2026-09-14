using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Reporting.Dtos;
using UniversalPOS.Domain.Restaurant;
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

    public async Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var rows = await CompletedSales(companyId, branchId, fromUtc, toUtc)
            .Select(s => new { s.CompletedAtUtc, s.GrandTotal })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => DateOnly.FromDateTime(r.CompletedAtUtc!.Value))
            .Select(g => new SalesTrendPointDto { Date = g.Key, NetSales = g.Sum(x => x.GrandTotal), TransactionCount = g.Count() })
            .OrderBy(x => x.Date)
            .ToList();
    }

    public async Task<IReadOnlyList<BranchComparisonDto>> GetBranchComparisonAsync(long companyId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var branches = await _db.Branches.Where(b => b.CompanyId == companyId).Select(b => new { b.Id, b.Name }).ToListAsync(cancellationToken);

        var results = new List<BranchComparisonDto>();
        foreach (var branch in branches)
        {
            var summary = await GetSalesSummaryAsync(companyId, branch.Id, fromUtc, toUtc, cancellationToken);
            results.Add(new BranchComparisonDto
            {
                BranchId = branch.Id,
                BranchName = branch.Name,
                NetSales = summary.NetSales,
                TransactionCount = summary.TransactionCount,
                AverageSaleValue = summary.AverageSaleValue,
            });
        }

        return results.OrderByDescending(r => r.NetSales).ToList();
    }

    public async Task<DiscountReportDto> GetDiscountReportAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var sales = await CompletedSales(companyId, branchId, fromUtc, toUtc)
            .Select(s => new { s.Id, s.DiscountTotal, s.CouponDiscountAmount })
            .ToListAsync(cancellationToken);

        var discountedSales = sales.Where(s => s.DiscountTotal > 0 || s.CouponDiscountAmount > 0).ToList();
        var lineDiscountTotal = sales.Sum(s => s.DiscountTotal);
        var couponDiscountTotal = sales.Sum(s => s.CouponDiscountAmount);

        var saleIds = sales.Select(s => s.Id).ToList();
        var topDiscountedProducts = await _db.SaleLines
            .Where(l => saleIds.Contains(l.SaleHeaderId) && l.LineDiscountAmount > 0)
            .Join(_db.Products, l => l.ProductId, p => p.Id, (l, p) => new { l.LineDiscountAmount, p.Id, p.Name })
            .GroupBy(x => new { x.Id, x.Name })
            .Select(g => new SalesByProductDto { ProductId = g.Key.Id, ProductName = g.Key.Name, Revenue = g.Sum(x => x.LineDiscountAmount) })
            .OrderByDescending(x => x.Revenue)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new DiscountReportDto
        {
            LineDiscountTotal = lineDiscountTotal,
            CouponDiscountTotal = couponDiscountTotal,
            SalesWithDiscountCount = discountedSales.Count,
            AverageDiscountPerDiscountedSale = discountedSales.Count > 0
                ? Domain.Sales.Money.Round(discountedSales.Sum(s => s.DiscountTotal + s.CouponDiscountAmount) / discountedSales.Count)
                : 0m,
            TopDiscountedProducts = topDiscountedProducts,
        };
    }

    public async Task<TaxReportDto> GetTaxReportAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var saleIds = CompletedSales(companyId, branchId, fromUtc, toUtc).Select(s => s.Id);

        var byRate = await _db.SaleLines
            .Where(l => saleIds.Contains(l.SaleHeaderId))
            .GroupBy(l => l.TaxRatePercentage)
            .Select(g => new TaxByRateDto
            {
                TaxRatePercentage = g.Key,
                TaxableRevenue = g.Sum(x => x.LineTotal),
                TaxCollected = g.Sum(x => x.LineTaxAmount),
            })
            .OrderByDescending(x => x.TaxCollected)
            .ToListAsync(cancellationToken);

        return new TaxReportDto
        {
            TotalTaxCollected = byRate.Sum(r => r.TaxCollected),
            ByRate = byRate,
        };
    }

    public async Task<IReadOnlyList<KitchenStationPerformanceDto>> GetKitchenPerformanceAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var tickets = await _db.PreparationTickets
            .Where(t => t.CompanyId == companyId && t.BranchId == branchId && t.CreatedAtUtc >= fromUtc && t.CreatedAtUtc < toUtc)
            .Select(t => new { t.KitchenStationId, t.Status, t.CreatedAtUtc, t.ReadyAtUtc })
            .ToListAsync(cancellationToken);

        var stationIds = tickets.Select(t => t.KitchenStationId).Distinct().ToList();
        var stationNames = await _db.KitchenStations.Where(s => stationIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        return tickets
            .GroupBy(t => t.KitchenStationId)
            .Select(g =>
            {
                var completed = g.Where(t => t.ReadyAtUtc.HasValue).ToList();
                return new KitchenStationPerformanceDto
                {
                    KitchenStationId = g.Key,
                    StationName = stationNames.GetValueOrDefault(g.Key, "(unknown)"),
                    TicketCount = g.Count(),
                    CancelledTicketCount = g.Count(t => t.Status == TicketStatus.Cancelled),
                    AveragePrepTimeMinutes = completed.Count > 0
                        ? Math.Round((decimal)completed.Average(t => (t.ReadyAtUtc!.Value - t.CreatedAtUtc).TotalMinutes), 1)
                        : null,
                };
            })
            .OrderByDescending(x => x.TicketCount)
            .ToList();
    }

    public async Task<IReadOnlyList<WaiterPerformanceDto>> GetWaiterPerformanceAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var orders = await _db.Orders
            .Where(o => o.CompanyId == companyId && o.BranchId == branchId && o.CreatedAtUtc >= fromUtc && o.CreatedAtUtc < toUtc)
            .Select(o => new { o.Id, o.CreatedByUserId, o.SaleHeaderId })
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(o => o.Id).ToList();
        var cancelledTicketsByOrder = await _db.PreparationTickets
            .Where(t => orderIds.Contains(t.OrderId) && t.Status == TicketStatus.Cancelled)
            .GroupBy(t => t.OrderId)
            .Select(g => new { OrderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrderId, x => x.Count, cancellationToken);

        var saleHeaderIds = orders.Where(o => o.SaleHeaderId.HasValue).Select(o => o.SaleHeaderId!.Value).ToList();
        var saleTotals = await _db.SaleHeaders
            .Where(s => saleHeaderIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.GrandTotal, cancellationToken);

        var userIds = orders.Select(o => o.CreatedByUserId).Distinct().ToList();
        var userNames = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return orders
            .GroupBy(o => o.CreatedByUserId)
            .Select(g => new WaiterPerformanceDto
            {
                WaiterUserId = g.Key,
                WaiterName = userNames.GetValueOrDefault(g.Key, "(unknown)"),
                OrderCount = g.Count(),
                CancelledTicketCount = g.Sum(o => cancelledTicketsByOrder.GetValueOrDefault(o.Id, 0)),
                BilledRevenue = g.Sum(o => o.SaleHeaderId.HasValue ? saleTotals.GetValueOrDefault(o.SaleHeaderId.Value, 0m) : 0m),
            })
            .OrderByDescending(x => x.BilledRevenue)
            .ToList();
    }

    public async Task<IReadOnlyList<SlowMovingStockDto>> GetSlowMovingStockAsync(long companyId, long branchId, int staleAfterDays = 30, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-staleAfterDays);

        var stocked = await _db.StockOnHands
            .Where(soh => soh.CompanyId == companyId && soh.BranchId == branchId && soh.QuantityOnHand > 0)
            .Join(_db.Products, soh => soh.ProductId, p => p.Id, (soh, p) => new { p.Id, p.Name, soh.QuantityOnHand })
            .ToListAsync(cancellationToken);

        var productIds = stocked.Select(s => s.Id).ToList();
        var lastSaleByProduct = await _db.SaleLines
            .Where(l => productIds.Contains(l.ProductId))
            .Join(_db.SaleHeaders.Where(s => s.CompanyId == companyId && s.BranchId == branchId && s.Status == SaleStatus.Completed),
                l => l.SaleHeaderId, s => s.Id, (l, s) => new { l.ProductId, s.CompletedAtUtc })
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, LastSoldAtUtc = g.Max(x => x.CompletedAtUtc) })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastSoldAtUtc, cancellationToken);

        var now = DateTime.UtcNow;
        return stocked
            .Select(s =>
            {
                var lastSold = lastSaleByProduct.GetValueOrDefault(s.Id);
                return new SlowMovingStockDto
                {
                    ProductId = s.Id,
                    ProductName = s.Name,
                    QuantityOnHand = s.QuantityOnHand,
                    LastSoldAtUtc = lastSold,
                    DaysSinceLastSale = lastSold.HasValue ? (int)(now - lastSold.Value).TotalDays : null,
                };
            })
            .Where(s => !s.LastSoldAtUtc.HasValue || s.LastSoldAtUtc.Value < cutoff)
            .OrderBy(s => s.LastSoldAtUtc ?? DateTime.MinValue)
            .ToList();
    }
}
