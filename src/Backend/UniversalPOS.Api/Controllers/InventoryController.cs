using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Inventory;
using UniversalPOS.Application.Inventory.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/branches/{branchId:long}/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IStockService _stockService;
    private readonly ICurrentUserService _currentUser;

    public InventoryController(IStockService stockService, ICurrentUserService currentUser)
    {
        _stockService = stockService;
        _currentUser = currentUser;
    }

    [HttpGet("stock-on-hand")]
    public async Task<IActionResult> GetStockOnHand(long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _stockService.GetStockOnHandAsync(_currentUser.CompanyId, branchId, cancellationToken));
    }

    [HttpGet("products/{productId:long}/ledger")]
    public async Task<IActionResult> GetLedger(long branchId, long productId, CancellationToken cancellationToken)
    {
        return Ok(await _stockService.GetLedgerAsync(_currentUser.CompanyId, branchId, productId, cancellationToken));
    }

    [HttpGet("adjustments/pending")]
    [RequirePermission(PermissionCodes.InventoryAdjust)]
    public async Task<IActionResult> GetPendingAdjustments(long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _stockService.GetPendingAdjustmentsAsync(_currentUser.CompanyId, branchId, cancellationToken));
    }

    [HttpPost("adjustments")]
    [RequirePermission(PermissionCodes.InventoryAdjust)]
    public async Task<IActionResult> CreateAdjustment(long branchId, [FromBody] CreateStockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new Application.Common.Exceptions.ForbiddenException("No authenticated user context.");
        var result = await _stockService.CreateAdjustmentAsync(_currentUser.CompanyId, branchId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetPendingAdjustments), new { branchId }, result);
    }

    [HttpPost("adjustments/{adjustmentId:long}/approve")]
    [RequirePermission(PermissionCodes.StockAdjustmentApprove)]
    public async Task<IActionResult> ApproveAdjustment(long branchId, long adjustmentId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new Application.Common.Exceptions.ForbiddenException("No authenticated user context.");
        var result = await _stockService.ApproveAdjustmentAsync(_currentUser.CompanyId, adjustmentId, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("transfers")]
    public async Task<IActionResult> GetTransfers(long branchId, CancellationToken cancellationToken)
        => Ok(await _stockService.GetTransfersAsync(_currentUser.CompanyId, branchId, cancellationToken));

    [HttpPost("transfers")]
    [RequirePermission(PermissionCodes.InventoryTransfer)]
    public async Task<IActionResult> CreateTransfer(long branchId, [FromBody] CreateStockTransferRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _stockService.CreateTransferAsync(_currentUser.CompanyId, branchId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetTransfers), new { branchId }, result);
    }

    [HttpPost("transfers/{transferId:long}/send")]
    [RequirePermission(PermissionCodes.InventoryTransfer)]
    public async Task<IActionResult> SendTransfer(long branchId, long transferId, CancellationToken cancellationToken)
        => Ok(await _stockService.SendTransferAsync(_currentUser.CompanyId, transferId, RequireUserId(), cancellationToken));

    [HttpPost("transfers/{transferId:long}/receive")]
    [RequirePermission(PermissionCodes.InventoryTransfer)]
    public async Task<IActionResult> ReceiveTransfer(long branchId, long transferId, CancellationToken cancellationToken)
        => Ok(await _stockService.ReceiveTransferAsync(_currentUser.CompanyId, transferId, RequireUserId(), cancellationToken));

    [HttpPost("counts")]
    [RequirePermission(PermissionCodes.InventoryCount)]
    public async Task<IActionResult> CreateCount(long branchId, [FromBody] CreateStockCountRequest request, CancellationToken cancellationToken)
    {
        var result = await _stockService.CreateCountAsync(_currentUser.CompanyId, branchId, RequireUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(CreateCount), new { branchId }, result);
    }

    [HttpPost("counts/{stockCountId:long}/lines")]
    [RequirePermission(PermissionCodes.InventoryCount)]
    public async Task<IActionResult> SubmitCountLines(long branchId, long stockCountId, [FromBody] List<SubmitStockCountLineRequest> lines, CancellationToken cancellationToken)
        => Ok(await _stockService.SubmitCountLinesAsync(_currentUser.CompanyId, stockCountId, lines, cancellationToken));

    [HttpPost("counts/{stockCountId:long}/complete")]
    [RequirePermission(PermissionCodes.InventoryCount)]
    public async Task<IActionResult> CompleteCount(long branchId, long stockCountId, CancellationToken cancellationToken)
        => Ok(await _stockService.CompleteCountAsync(_currentUser.CompanyId, stockCountId, RequireUserId(), cancellationToken));

    private long RequireUserId() => _currentUser.UserId ?? throw new Application.Common.Exceptions.ForbiddenException("No authenticated user context.");
}
