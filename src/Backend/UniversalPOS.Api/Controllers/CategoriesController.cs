using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Catalog;
using UniversalPOS.Application.Catalog.Dtos;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ICurrentUserService _currentUser;

    public CategoriesController(ICatalogService catalogService, ICurrentUserService currentUser)
    {
        _catalogService = catalogService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetCategoriesAsync(_currentUser.CompanyId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.ProductManage)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateCategoryAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetCategories), new { }, result);
    }
}
