using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Sales;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/branches/{branchId:long}/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;
    private readonly ICurrentUserService _currentUser;

    public SalesController(ISalesService salesService, ICurrentUserService currentUser)
    {
        _salesService = salesService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission(PermissionCodes.ReportsViewSales)]
    public async Task<IActionResult> GetSales(long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _salesService.GetSalesAsync(_currentUser.CompanyId, branchId, cancellationToken));
    }

    [HttpGet("{saleId:long}")]
    public async Task<IActionResult> GetSale(long branchId, long saleId, CancellationToken cancellationToken)
    {
        return Ok(await _salesService.GetSaleAsync(_currentUser.CompanyId, saleId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.SalesCreate)]
    public async Task<IActionResult> Checkout(long branchId, [FromBody] CreateSaleRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _salesService.CheckoutAsync(_currentUser.CompanyId, branchId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetSale), new { branchId, saleId = result.Id }, result);
    }

    [HttpPost("{saleId:long}/void")]
    [RequirePermission(PermissionCodes.SalesVoid)]
    public async Task<IActionResult> Void(long branchId, long saleId, [FromBody] VoidSaleRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _salesService.VoidSaleAsync(_currentUser.CompanyId, branchId, _currentUser.TerminalId, userId, saleId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("held")]
    public async Task<IActionResult> GetHeldBills(long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _salesService.GetHeldBillsAsync(_currentUser.CompanyId, branchId, cancellationToken));
    }

    [HttpPost("held")]
    public async Task<IActionResult> Hold(long branchId, [FromBody] CreateHeldBillRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _salesService.HoldAsync(_currentUser.CompanyId, branchId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetHeldBills), new { branchId }, result);
    }

    [HttpPost("held/{heldBillId:long}/recall")]
    public async Task<IActionResult> Recall(long branchId, long heldBillId, CancellationToken cancellationToken)
    {
        return Ok(await _salesService.RecallAsync(_currentUser.CompanyId, heldBillId, cancellationToken));
    }

    [HttpDelete("held/{heldBillId:long}")]
    public async Task<IActionResult> DeleteHeldBill(long branchId, long heldBillId, CancellationToken cancellationToken)
    {
        await _salesService.DeleteHeldBillAsync(_currentUser.CompanyId, heldBillId, cancellationToken);
        return NoContent();
    }

    private long RequireUserId() => _currentUser.UserId ?? throw new ForbiddenException("No authenticated user context.");
}
