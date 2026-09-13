using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Api.Authorization;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Restaurant;
using UniversalPOS.Application.Restaurant.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/branches/{branchId:long}")]
[Authorize]
public class RestaurantController : ControllerBase
{
    private readonly IRestaurantService _restaurantService;
    private readonly ICurrentUserService _currentUser;

    public RestaurantController(IRestaurantService restaurantService, ICurrentUserService currentUser)
    {
        _restaurantService = restaurantService;
        _currentUser = currentUser;
    }

    [HttpGet("floors")]
    public async Task<IActionResult> GetFloors(long branchId, CancellationToken cancellationToken)
        => Ok(await _restaurantService.GetFloorsAsync(_currentUser.CompanyId, branchId, cancellationToken));

    [HttpPost("tables/{tableId:long}/open")]
    [RequirePermission(PermissionCodes.OrderCreate)]
    public async Task<IActionResult> OpenTable(long branchId, long tableId, [FromBody] OpenTableRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _restaurantService.OpenTableAsync(_currentUser.CompanyId, branchId, tableId, userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { branchId, orderId = result.Id }, result);
    }

    [HttpGet("orders/{orderId:long}")]
    public async Task<IActionResult> GetOrder(long branchId, long orderId, CancellationToken cancellationToken)
        => Ok(await _restaurantService.GetOrderAsync(_currentUser.CompanyId, orderId, cancellationToken));

    [HttpPost("orders/{orderId:long}/lines")]
    [RequirePermission(PermissionCodes.OrderCreate)]
    public async Task<IActionResult> AddLines(long branchId, long orderId, [FromBody] List<AddOrderLineRequest> lines, CancellationToken cancellationToken)
        => Ok(await _restaurantService.AddOrderLinesAsync(_currentUser.CompanyId, orderId, lines, cancellationToken));

    [HttpPost("orders/{orderId:long}/send-to-kitchen")]
    [RequirePermission(PermissionCodes.OrderCreate)]
    public async Task<IActionResult> SendToKitchen(long branchId, long orderId, CancellationToken cancellationToken)
        => Ok(await _restaurantService.SendToKitchenAsync(_currentUser.CompanyId, branchId, orderId, cancellationToken));

    [HttpPost("orders/{orderId:long}/bill")]
    [RequirePermission(PermissionCodes.OrderBill)]
    public async Task<IActionResult> BillOrder(long branchId, long orderId, [FromBody] BillOrderRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        return Ok(await _restaurantService.BillOrderAsync(_currentUser.CompanyId, branchId, userId, orderId, request, cancellationToken));
    }

    [HttpGet("kitchen-stations")]
    public async Task<IActionResult> GetKitchenStations(long branchId, CancellationToken cancellationToken)
        => Ok(await _restaurantService.GetKitchenStationsAsync(_currentUser.CompanyId, branchId, cancellationToken));

    [HttpGet("kitchen-stations/{stationId:long}/tickets")]
    public async Task<IActionResult> GetKdsTickets(long branchId, long stationId, CancellationToken cancellationToken)
        => Ok(await _restaurantService.GetKdsTicketsAsync(_currentUser.CompanyId, branchId, stationId, cancellationToken));

    [HttpPost("tickets/{ticketId:long}/status")]
    [RequirePermission(PermissionCodes.KdsUpdate)]
    public async Task<IActionResult> UpdateTicketStatus(long branchId, long ticketId, [FromBody] UpdateTicketStatusRequest request, CancellationToken cancellationToken)
        => Ok(await _restaurantService.UpdateTicketStatusAsync(_currentUser.CompanyId, ticketId, request, cancellationToken));

    private long RequireUserId() => _currentUser.UserId ?? throw new ForbiddenException("No authenticated user context.");
}
