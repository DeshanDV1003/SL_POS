using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Reporting;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/branches/{branchId:long}/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly ICurrentUserService _currentUser;

    public ReportsController(IReportingService reportingService, ICurrentUserService currentUser)
    {
        _reportingService = reportingService;
        _currentUser = currentUser;
    }

    private static (DateTime FromUtc, DateTime ToUtc) ResolveRange(DateTime? from, DateTime? to)
    {
        var toUtc = (to ?? DateTime.UtcNow.Date.AddDays(1)).ToUniversalTime();
        var fromUtc = (from ?? toUtc.AddDays(-7)).ToUniversalTime();
        return (fromUtc, toUtc);
    }

    /// <summary>Returns rows as a downloadable CSV file when ?format=csv is present, otherwise the caller's own Ok(rows) result.</summary>
    private IActionResult RowsOrCsv<T>(IReadOnlyList<T> rows, string? format, string fileName)
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = ReportCsvWriter.Write(rows);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"{fileName}.csv");
        }
        return Ok(rows);
    }

    [HttpGet("sales-summary")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetSalesSummary(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        return Ok(await _reportingService.GetSalesSummaryAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken));
    }

    [HttpGet("sales-by-product")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetSalesByProduct(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _reportingService.GetSalesByProductAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken);
        return RowsOrCsv(rows, format, "sales-by-product");
    }

    [HttpGet("sales-by-category")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetSalesByCategory(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _reportingService.GetSalesByCategoryAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken);
        return RowsOrCsv(rows, format, "sales-by-category");
    }

    [HttpGet("sales-by-cashier")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetSalesByCashier(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _reportingService.GetSalesByCashierAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken);
        return RowsOrCsv(rows, format, "sales-by-cashier");
    }

    [HttpGet("sales-by-payment-method")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetSalesByPaymentMethod(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _reportingService.GetSalesByPaymentMethodAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken);
        return RowsOrCsv(rows, format, "sales-by-payment-method");
    }

    [HttpGet("sales-trend")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetSalesTrend(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _reportingService.GetSalesTrendAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken);
        return RowsOrCsv(rows, format, "sales-trend");
    }

    [HttpGet("~/api/v1/reports/branch-comparison")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetBranchComparison([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _reportingService.GetBranchComparisonAsync(_currentUser.CompanyId, fromUtc, toUtc, cancellationToken);
        return RowsOrCsv(rows, format, "branch-comparison");
    }

    [HttpGet("discount-report")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetDiscountReport(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        return Ok(await _reportingService.GetDiscountReportAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken));
    }

    [HttpGet("tax-report")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetTaxReport(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var report = await _reportingService.GetTaxReportAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken);
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = ReportCsvWriter.Write(report.ByRate);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "tax-report.csv");
        }
        return Ok(report);
    }

    [HttpGet("kitchen-performance")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetKitchenPerformance(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _reportingService.GetKitchenPerformanceAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken);
        return RowsOrCsv(rows, format, "kitchen-performance");
    }

    [HttpGet("waiter-performance")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetWaiterPerformance(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _reportingService.GetWaiterPerformanceAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken);
        return RowsOrCsv(rows, format, "waiter-performance");
    }

    [HttpGet("slow-moving-stock")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetSlowMovingStock(long branchId, [FromQuery] int staleAfterDays, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var rows = await _reportingService.GetSlowMovingStockAsync(_currentUser.CompanyId, branchId, staleAfterDays <= 0 ? 30 : staleAfterDays, cancellationToken);
        return RowsOrCsv(rows, format, "slow-moving-stock");
    }

    [HttpGet("stock-valuation")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetStockValuation(long branchId, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        var rows = await _reportingService.GetStockValuationAsync(_currentUser.CompanyId, branchId, cancellationToken);
        return RowsOrCsv(rows, format, "stock-valuation");
    }

    [HttpGet("~/api/v1/branches/{branchId:long}/dashboard")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetDashboard(long branchId, CancellationToken cancellationToken)
        => Ok(await _reportingService.GetDashboardSummaryAsync(_currentUser.CompanyId, branchId, cancellationToken));
}
