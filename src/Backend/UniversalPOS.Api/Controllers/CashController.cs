using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Cash;
using UniversalPOS.Application.Cash.Dtos;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/branches/{branchId:long}")]
[Authorize]
public class CashController : ControllerBase
{
    private readonly ICashService _cashService;
    private readonly ICurrentUserService _currentUser;

    public CashController(ICashService cashService, ICurrentUserService currentUser)
    {
        _cashService = cashService;
        _currentUser = currentUser;
    }

    [HttpPost("shifts")]
    [RequirePermission(PermissionCodes.CashShiftOpen)]
    public async Task<IActionResult> OpenShift(long branchId, [FromBody] OpenShiftRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _cashService.OpenShiftAsync(_currentUser.CompanyId, branchId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetShift), new { branchId, shiftId = result.Id }, result);
    }

    [HttpGet("shifts/{shiftId:long}")]
    public async Task<IActionResult> GetShift(long branchId, long shiftId, CancellationToken cancellationToken)
        => Ok(await _cashService.GetShiftAsync(_currentUser.CompanyId, shiftId, cancellationToken));

    [HttpGet("shifts/open")]
    public async Task<IActionResult> GetOpenShift(long branchId, [FromQuery] long terminalId, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var shift = await _cashService.GetOpenShiftAsync(_currentUser.CompanyId, terminalId, userId, cancellationToken);
        return shift is null ? NotFound() : Ok(shift);
    }

    [HttpPost("shifts/{shiftId:long}/movements")]
    [RequirePermission(PermissionCodes.CashMovementCreate)]
    public async Task<IActionResult> RecordMovement(long branchId, long shiftId, [FromBody] RecordCashMovementRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        return Ok(await _cashService.RecordMovementAsync(_currentUser.CompanyId, shiftId, userId, request, cancellationToken));
    }

    [HttpPost("shifts/{shiftId:long}/close")]
    [RequirePermission(PermissionCodes.CashShiftClose)]
    public async Task<IActionResult> CloseShift(long branchId, long shiftId, [FromBody] CloseShiftRequest request, CancellationToken cancellationToken)
        => Ok(await _cashService.CloseShiftAsync(_currentUser.CompanyId, shiftId, request, cancellationToken));

    [HttpGet("day-end-report")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetDayEndReport(long branchId, [FromQuery] DateOnly businessDate, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        return Ok(await _cashService.GenerateDayEndReportAsync(_currentUser.CompanyId, branchId, businessDate, userId, cancellationToken));
    }

    [HttpPost("day-end-report/{reportId:long}/finalize")]
    [RequirePermission(PermissionCodes.DayEndReportFinalize)]
    public async Task<IActionResult> FinalizeDayEndReport(long branchId, long reportId, CancellationToken cancellationToken)
        => Ok(await _cashService.FinalizeDayEndReportAsync(_currentUser.CompanyId, reportId, cancellationToken));

    [HttpGet("day-end-report/history")]
    [RequirePermission(PermissionCodes.ReportsViewFinancial)]
    public async Task<IActionResult> GetDayEndReportHistory(long branchId, CancellationToken cancellationToken)
        => Ok(await _cashService.GetDayEndReportHistoryAsync(_currentUser.CompanyId, branchId, cancellationToken));

    [HttpPost("cash-drawer/open")]
    [RequirePermission(PermissionCodes.CashDrawerOpen)]
    public async Task<IActionResult> OpenCashDrawer(long branchId, CancellationToken cancellationToken)
    {
        await _cashService.OpenCashDrawerAsync(_currentUser.CompanyId, branchId, _currentUser.TerminalId, RequireUserId(), cancellationToken);
        return NoContent();
    }

    private long RequireUserId() => _currentUser.UserId ?? throw new ForbiddenException("No authenticated user context.");
}
