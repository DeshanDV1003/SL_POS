using UniversalPOS.Application.Identity.Dtos;

namespace UniversalPOS.Application.Identity;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<LoginResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
