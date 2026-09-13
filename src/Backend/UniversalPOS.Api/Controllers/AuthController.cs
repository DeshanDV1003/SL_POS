using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversalPOS.Application.Identity;
using UniversalPOS.Application.Identity.Dtos;

namespace UniversalPOS.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(request, ip, cancellationToken);
        return Ok(result);
    }

    public record RefreshRequest(string RefreshToken);

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshAsync(request.RefreshToken, ip, cancellationToken);
        return Ok(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await _authService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }
}
