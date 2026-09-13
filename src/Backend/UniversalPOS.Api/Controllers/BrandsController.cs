using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Catalog;
using UniversalPOS.Application.Catalog.Dtos;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/brands")]
[Authorize]
public class BrandsController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ICurrentUserService _currentUser;

    public BrandsController(ICatalogService catalogService, ICurrentUserService currentUser)
    {
        _catalogService = catalogService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetBrands(CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetBrandsAsync(_currentUser.CompanyId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.ProductManage)]
    public async Task<IActionResult> CreateBrand([FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateBrandAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetBrands), new { }, result);
    }
}
