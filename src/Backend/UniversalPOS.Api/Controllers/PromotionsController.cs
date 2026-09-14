using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Sales;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/promotions")]
[Authorize]
public class PromotionsController : ControllerBase
{
    private readonly IPromotionService _promotionService;
    private readonly ICurrentUserService _currentUser;

    public PromotionsController(IPromotionService promotionService, ICurrentUserService currentUser)
    {
        _promotionService = promotionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPromotions(CancellationToken cancellationToken)
        => Ok(await _promotionService.GetPromotionsAsync(_currentUser.CompanyId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.PromotionManage)]
    public async Task<IActionResult> CreatePromotion([FromBody] CreatePromotionRequest request, CancellationToken cancellationToken)
    {
        var result = await _promotionService.CreatePromotionAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetPromotions), new { }, result);
    }

    [HttpGet("~/api/v1/coupons")]
    public async Task<IActionResult> GetCoupons(CancellationToken cancellationToken)
        => Ok(await _promotionService.GetCouponsAsync(_currentUser.CompanyId, cancellationToken));

    [HttpPost("~/api/v1/coupons")]
    [RequirePermission(PermissionCodes.PromotionManage)]
    public async Task<IActionResult> CreateCoupon([FromBody] CreateCouponRequest request, CancellationToken cancellationToken)
    {
        var result = await _promotionService.CreateCouponAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetCoupons), new { }, result);
    }
}
