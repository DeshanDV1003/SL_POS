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

    [HttpGet("sales-summary")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetSalesSummary(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        return Ok(await _reportingService.GetSalesSummaryAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken));
    }

    [HttpGet("sales-by-product")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetSalesByProduct(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        return Ok(await _reportingService.GetSalesByProductAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken));
    }

    [HttpGet("sales-by-category")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetSalesByCategory(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        return Ok(await _reportingService.GetSalesByCategoryAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken));
    }

    [HttpGet("sales-by-cashier")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetSalesByCashier(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        return Ok(await _reportingService.GetSalesByCashierAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken));
    }

    [HttpGet("sales-by-payment-method")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetSalesByPaymentMethod(long branchId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        return Ok(await _reportingService.GetSalesByPaymentMethodAsync(_currentUser.CompanyId, branchId, fromUtc, toUtc, cancellationToken));
    }

    [HttpGet("stock-valuation")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetStockValuation(long branchId, CancellationToken cancellationToken)
        => Ok(await _reportingService.GetStockValuationAsync(_currentUser.CompanyId, branchId, cancellationToken));

    [HttpGet("~/api/v1/branches/{branchId:long}/dashboard")]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetDashboard(long branchId, CancellationToken cancellationToken)
        => Ok(await _reportingService.GetDashboardSummaryAsync(_currentUser.CompanyId, branchId, cancellationToken));
}
