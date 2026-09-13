using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Catalog;
using UniversalPOS.Application.Catalog.Dtos;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/tax-rates")]
[Authorize]
public class TaxRatesController : ControllerBase
{
    private readonly ICatalogService _catalogService;
    private readonly ICurrentUserService _currentUser;

    public TaxRatesController(ICatalogService catalogService, ICurrentUserService currentUser)
    {
        _catalogService = catalogService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetTaxRates(CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetTaxRatesAsync(_currentUser.CompanyId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.TaxRateManage)]
    public async Task<IActionResult> CreateTaxRate([FromBody] CreateTaxRateRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateTaxRateAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetTaxRates), new { }, result);
    }
}
