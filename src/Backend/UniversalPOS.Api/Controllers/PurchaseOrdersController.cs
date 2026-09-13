using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Purchasing;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/branches/{branchId:long}/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly ICurrentUserService _currentUser;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService, ICurrentUserService currentUser)
    {
        _purchaseOrderService = purchaseOrderService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPurchaseOrders(long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _purchaseOrderService.GetAsync(_currentUser.CompanyId, branchId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.PurchaseOrderCreate)]
    public async Task<IActionResult> CreatePurchaseOrder(long branchId, [FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("No authenticated user context.");
        var result = await _purchaseOrderService.CreateAsync(_currentUser.CompanyId, branchId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetPurchaseOrders), new { branchId }, result);
    }

    [HttpPost("{purchaseOrderId:long}/approve")]
    [RequirePermission(PermissionCodes.PurchaseOrderApprove)]
    public async Task<IActionResult> ApprovePurchaseOrder(long branchId, long purchaseOrderId, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("No authenticated user context.");
        var result = await _purchaseOrderService.ApproveAsync(_currentUser.CompanyId, purchaseOrderId, userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("~/api/v1/branches/{branchId:long}/goods-received-notes")]
    public async Task<IActionResult> GetGoodsReceivedNotes(long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _purchaseOrderService.GetGoodsReceivedNotesAsync(_currentUser.CompanyId, branchId, cancellationToken));
    }

    [HttpPost("~/api/v1/branches/{branchId:long}/goods-received-notes")]
    [RequirePermission(PermissionCodes.GoodsReceiptCreate)]
    public async Task<IActionResult> ReceiveGoods(long branchId, [FromBody] ReceiveGoodsRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("No authenticated user context.");
        var result = await _purchaseOrderService.ReceiveGoodsAsync(_currentUser.CompanyId, branchId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetGoodsReceivedNotes), new { branchId }, result);
    }

    [HttpGet("~/api/v1/branches/{branchId:long}/purchase-invoices")]
    public async Task<IActionResult> GetInvoices(long branchId, CancellationToken cancellationToken)
        => Ok(await _purchaseOrderService.GetInvoicesAsync(_currentUser.CompanyId, branchId, cancellationToken));

    [HttpPost("~/api/v1/branches/{branchId:long}/purchase-invoices")]
    [RequirePermission(PermissionCodes.PurchaseInvoiceCreate)]
    public async Task<IActionResult> CreateInvoice(long branchId, [FromBody] CreatePurchaseInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.CreateInvoiceAsync(_currentUser.CompanyId, branchId, request, cancellationToken);
        return CreatedAtAction(nameof(GetInvoices), new { branchId }, result);
    }

    [HttpPost("~/api/v1/branches/{branchId:long}/purchase-invoices/{invoiceId:long}/payments")]
    [RequirePermission(PermissionCodes.SupplierPaymentCreate)]
    public async Task<IActionResult> RecordPayment(long branchId, long invoiceId, [FromBody] RecordSupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("No authenticated user context.");
        var result = await _purchaseOrderService.RecordPaymentAsync(_currentUser.CompanyId, invoiceId, userId, request, cancellationToken);
        return Ok(result);
    }
}
