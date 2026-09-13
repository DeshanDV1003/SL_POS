using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Organization;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly IOrganizationQueryService _organizationQueryService;

    public CompaniesController(IOrganizationQueryService organizationQueryService)
    {
        _organizationQueryService = organizationQueryService;
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
}
