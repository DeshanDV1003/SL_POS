using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Crm;
using UniversalPOS.Application.Crm.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/membership-tiers")]
[Authorize]
public class MembershipTiersController : ControllerBase
{
    private readonly ILoyaltyService _loyaltyService;
    private readonly ICurrentUserService _currentUser;

    public MembershipTiersController(ILoyaltyService loyaltyService, ICurrentUserService currentUser)
    {
        _loyaltyService = loyaltyService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetTiers(CancellationToken cancellationToken)
        => Ok(await _loyaltyService.GetTiersAsync(_currentUser.CompanyId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCodes.CustomerManage)]
    public async Task<IActionResult> CreateTier([FromBody] CreateMembershipTierRequest request, CancellationToken cancellationToken)
    {
        var result = await _loyaltyService.CreateTierAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetTiers), new { }, result);
    }
}
