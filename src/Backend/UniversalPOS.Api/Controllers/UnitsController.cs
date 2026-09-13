using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Catalog;
using UniversalPOS.Application.Catalog.Dtos;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/units")]
[Authorize]
public class UnitsController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ICurrentUserService _currentUser;

    public UnitsController(ICatalogService catalogService, ICurrentUserService currentUser)
    {
        _catalogService = catalogService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetUnits(CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetUnitsAsync(_currentUser.CompanyId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.ProductManage)]
    public async Task<IActionResult> CreateUnit([FromBody] CreateUnitRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateUnitAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetUnits), new { }, result);
    }
}
