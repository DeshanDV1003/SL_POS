using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Crm;
using UniversalPOS.Application.Crm.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/customers/{customerId:long}/loyalty")]
[Authorize]
public class LoyaltyController : ControllerBase
{
    private readonly ILoyaltyService _loyaltyService;
    private readonly ICurrentUserService _currentUser;

    public LoyaltyController(ILoyaltyService loyaltyService, ICurrentUserService currentUser)
    {
        _loyaltyService = loyaltyService;
        _currentUser = currentUser;
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(long customerId, CancellationToken cancellationToken)
        => Ok(await _loyaltyService.GetHistoryAsync(_currentUser.CompanyId, customerId, cancellationToken));

    [HttpPost("redeem")]
    [RequirePermission(PermissionCodes.SalesCreate)]
    public async Task<IActionResult> Redeem(long customerId, [FromBody] RedeemPointsRequest request, CancellationToken cancellationToken)
    {
        await _loyaltyService.RedeemPointsAsync(_currentUser.CompanyId, customerId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("adjust")]
    [RequirePermission(PermissionCodes.LoyaltyAdjust)]
    public async Task<IActionResult> Adjust(long customerId, [FromBody] AdjustLoyaltyPointsRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("No authenticated user context.");
        await _loyaltyService.AdjustPointsAsync(_currentUser.CompanyId, customerId, userId, request, cancellationToken);
        return NoContent();
    }
}
