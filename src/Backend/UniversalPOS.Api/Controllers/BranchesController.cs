using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Application.Organization;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IOrganizationQueryService _organizationQueryService;

    public BranchesController(IOrganizationQueryService organizationQueryService)
    {
        _organizationQueryService = organizationQueryService;
    }

    [HttpGet("{branchId:long}/terminals")]
    public async Task<IActionResult> GetTerminals(long branchId, CancellationToken cancellationToken)
    {
        var terminals = await _organizationQueryService.GetTerminalsAsync(branchId, cancellationToken);
        return Ok(terminals);
    }
}
