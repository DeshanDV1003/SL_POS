using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Purchasing;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly ICurrentUserService _currentUser;

    public SuppliersController(ISupplierService supplierService, ICurrentUserService currentUser)
    {
        _supplierService = supplierService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetSuppliers(CancellationToken cancellationToken)
    {
        return Ok(await _supplierService.GetSuppliersAsync(_currentUser.CompanyId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.SupplierManage)]
    public async Task<IActionResult> CreateSupplier([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var result = await _supplierService.CreateSupplierAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetSuppliers), new { }, result);
    }
}
