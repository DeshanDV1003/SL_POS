using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Organization;
using UniversalPOS.Application.Organization.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ICurrentUserService _currentUser;

    public BranchesController(IOrganizationQueryService organizationQueryService, ICurrentUserService currentUser)
    {
        _organizationQueryService = organizationQueryService;
        _currentUser = currentUser;
    }

    [HttpGet("{branchId:long}/terminals")]
    public async Task<IActionResult> GetTerminals(long branchId, CancellationToken cancellationToken)
    {
        var terminals = await _organizationQueryService.GetTerminalsAsync(branchId, cancellationToken);
        return Ok(terminals);
    }

    [HttpPut("{branchId:long}/cash-settings")]
    [RequirePermission(PermissionCodes.BranchManage)]
    public async Task<IActionResult> UpdateCashSettings(long branchId, [FromBody] UpdateBranchCashSettingsRequest request, CancellationToken cancellationToken)
        => Ok(await _organizationQueryService.UpdateBranchCashSettingsAsync(_currentUser.CompanyId, branchId, request, cancellationToken));

    /// <summary>
    /// Any authenticated user on the terminal may call this — it just proves the
    /// terminal is currently online, not a privileged action. A frontend offline
    /// queue calls this on reconnect (and periodically while connected) so
    /// Terminal.LastSeenAtUtc reflects reality rather than only the last login.
    /// </summary>
    [HttpPost("{branchId:long}/terminals/{terminalId:long}/heartbeat")]
    public async Task<IActionResult> RecordHeartbeat(long branchId, long terminalId, CancellationToken cancellationToken)
    {
        await _organizationQueryService.RecordTerminalHeartbeatAsync(terminalId, cancellationToken);
        return NoContent();
    }
}
