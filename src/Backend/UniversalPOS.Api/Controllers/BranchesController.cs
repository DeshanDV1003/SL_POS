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
}
