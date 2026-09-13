using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Catalog;
using UniversalPOS.Application.Catalog.Dtos;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ICurrentUserService _currentUser;

    public ProductsController(ICatalogService catalogService, ICurrentUserService currentUser)
    {
        _catalogService = catalogService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetProductsAsync(_currentUser.CompanyId, search, cancellationToken));
    }

    [HttpGet("by-barcode/{barcode}")]
    public async Task<IActionResult> FindByBarcode(string barcode, CancellationToken cancellationToken)
    {
        var product = await _catalogService.FindByBarcodeAsync(_currentUser.CompanyId, barcode, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.ProductManage)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateProductAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetProducts), new { }, result);
    }
}
