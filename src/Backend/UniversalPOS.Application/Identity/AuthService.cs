using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Application.Identity;

public class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IDateTimeProvider _clock;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IDateTimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _clock = clock;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsSplitQuery()
            .Include(u => u.UserBranches)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user is null)
        {
            // Deliberately identical failure path to a wrong-password attempt: never reveal whether a username exists.
            throw new InvalidCredentialsException();
        }

        if (user.IsLockedOut && user.LockedOutUntilUtc.HasValue)
        {
            if (user.LockedOutUntilUtc.Value > _clock.UtcNow)
            {
                throw new AccountLockedOutException(user.LockedOutUntilUtc.Value);
            }

            // Lockout window has elapsed: reset before evaluating this attempt.
            user.IsLockedOut = false;
            user.FailedLoginCount = 0;
            user.LockedOutUntilUtc = null;
        }

        if (!user.IsActive || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.IsLockedOut = true;
                user.LockedOutUntilUtc = _clock.UtcNow.Add(LockoutDuration);
            }
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }

        user.FailedLoginCount = 0;
        user.LastLoginAtUtc = _clock.UtcNow;

        long? terminalId = null;
        if (!string.IsNullOrWhiteSpace(request.TerminalCode) && !string.IsNullOrWhiteSpace(request.BranchCode))
        {
            // Terminal.Code is unique only per-Branch (every branch may have its own "T1"),
            // so BranchCode is required to disambiguate which terminal is meant.
            terminalId = await _db.Terminals
                .Where(t => t.Code == request.TerminalCode && t.IsActive && t.Branch.Code == request.BranchCode)
                .Select(t => (long?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var permissionCodes = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Distinct()
            .ToList();

        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissionCodes, terminalId);
        var (refreshTokenValue, refreshTokenHash) = GenerateRefreshTokenPair();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TerminalId = terminalId,
            TokenHash = refreshTokenHash,
            ExpiresAtUtc = _clock.UtcNow.Add(RefreshTokenLifetime),
            CreatedAtUtc = _clock.UtcNow,
            CreatedByIp = ipAddress,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new LoginResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAtUtc = _clock.UtcNow.Add(_jwtTokenService.AccessTokenLifetime),
            User = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                CompanyId = user.CompanyId,
                BranchIds = user.UserBranches.Select(ub => ub.BranchId).ToList(),
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                Permissions = permissionCodes,
            },
        };
    }

    public async Task<LoginResult> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);

        var existing = await _db.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (existing is null)
        {
            throw new InvalidCredentialsException();
        }

        if (existing.IsRevoked)
        {
            // Reuse of an already-rotated/revoked token: treat as compromise and revoke the whole chain for this user.
            var allActive = await _db.RefreshTokens
                .Where(t => t.UserId == existing.UserId && !t.IsRevoked)
                .ToListAsync(cancellationToken);
            foreach (var t in allActive)
            {
                t.IsRevoked = true;
            }
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (existing.ExpiresAtUtc <= _clock.UtcNow)
        {
            throw new InvalidCredentialsException();
        }

        var user = await _db.Users
            .AsSplitQuery()
            .Include(u => u.UserBranches)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstAsync(u => u.Id == existing.UserId, cancellationToken);

        if (!user.IsActive)
        {
            throw new InvalidCredentialsException();
        }

        existing.IsRevoked = true;

        var permissionCodes = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Distinct()
            .ToList();

        var accessToken = _jwtTokenService.GenerateAccessToken(user, permissionCodes, existing.TerminalId);
        var (newRefreshTokenValue, newRefreshTokenHash) = GenerateRefreshTokenPair();

        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            TerminalId = existing.TerminalId,
            TokenHash = newRefreshTokenHash,
            ExpiresAtUtc = _clock.UtcNow.Add(RefreshTokenLifetime),
            CreatedAtUtc = _clock.UtcNow,
            CreatedByIp = ipAddress,
        };
        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        existing.ReplacedByTokenId = newRefreshToken.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return new LoginResult
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshTokenValue,
            AccessTokenExpiresAtUtc = _clock.UtcNow.Add(_jwtTokenService.AccessTokenLifetime),
            User = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                CompanyId = user.CompanyId,
                BranchIds = user.UserBranches.Select(ub => ub.BranchId).ToList(),
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                Permissions = permissionCodes,
            },
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var existing = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (existing is not null && !existing.IsRevoked)
        {
            existing.IsRevoked = true;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static (string Value, string Hash) GenerateRefreshTokenPair()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var value = Convert.ToBase64String(bytes);
        return (value, HashToken(value));
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
