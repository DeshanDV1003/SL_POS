using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Crm;
using UniversalPOS.Application.Crm.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ICurrentUserService _currentUser;

    public CustomersController(ICustomerService customerService, ICurrentUserService currentUser)
    {
        _customerService = customerService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return Ok(await _customerService.GetCustomersAsync(_currentUser.CompanyId, search, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CustomerManage)]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await _customerService.CreateCustomerAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetCustomers), new { }, result);
    }

    [HttpGet("groups")]
    public async Task<IActionResult> GetCustomerGroups(CancellationToken cancellationToken)
    {
        return Ok(await _customerService.GetCustomerGroupsAsync(_currentUser.CompanyId, cancellationToken));
    }

    [HttpPost("groups")]
    [RequirePermission(PermissionCodes.CustomerManage)]
    public async Task<IActionResult> CreateCustomerGroup([FromBody] CreateCustomerGroupRequest request, CancellationToken cancellationToken)
    {
        var result = await _customerService.CreateCustomerGroupAsync(_currentUser.CompanyId, request, cancellationToken);
        return CreatedAtAction(nameof(GetCustomerGroups), new { }, result);
    }
}
