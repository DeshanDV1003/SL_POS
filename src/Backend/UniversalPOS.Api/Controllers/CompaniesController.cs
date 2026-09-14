using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Crm;
using UniversalPOS.Application.Organization;
using UniversalPOS.Application.Organization.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly IOrganizationQueryService _organizationQueryService;
    private readonly ILoyaltyService _loyaltyService;

    public CompaniesController(IOrganizationQueryService organizationQueryService, ILoyaltyService loyaltyService)
    {
        _organizationQueryService = organizationQueryService;
        _loyaltyService = loyaltyService;
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.CompanyManage)]
    public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken)
    {
        var companies = await _organizationQueryService.GetCompaniesAsync(cancellationToken);
        return Ok(companies);
    }

    [HttpGet("{companyId:long}/branches")]
    public async Task<IActionResult> GetBranches(long companyId, CancellationToken cancellationToken)
    {
        var branches = await _organizationQueryService.GetBranchesAsync(companyId, cancellationToken);
        return Ok(branches);
    }

    [HttpPut("{companyId:long}/loyalty-settings")]
    [RequirePermission(PermissionCodes.CompanyManage)]
    public async Task<IActionResult> UpdateLoyaltySettings(long companyId, [FromBody] UpdateCompanyLoyaltySettingsRequest request, CancellationToken cancellationToken)
        => Ok(await _organizationQueryService.UpdateCompanyLoyaltySettingsAsync(companyId, request, cancellationToken));

    /// <summary>
    /// Admin-triggered points expiry, in addition to the daily PointsExpiryBackgroundService —
    /// lets this be exercised (and tested) on demand rather than only waiting for the schedule.
    /// </summary>
    [HttpPost("{companyId:long}/loyalty/expire-points")]
    [RequirePermission(PermissionCodes.CompanyManage)]
    public async Task<IActionResult> ExpireLoyaltyPoints(long companyId, CancellationToken cancellationToken)
    {
        var customersAffected = await _loyaltyService.ExpirePointsAsync(companyId, cancellationToken);
        return Ok(new { customersAffected });
    }
}
