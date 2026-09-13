using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_options.AccessTokenLifetimeMinutes);

    public string GenerateAccessToken(AppUser user, IReadOnlyCollection<string> permissionCodes, long? terminalId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("username", user.Username),
            new("company_id", user.CompanyId.ToString()),
        };

        if (terminalId.HasValue)
        {
            claims.Add(new Claim("terminal_id", terminalId.Value.ToString()));
        }

        claims.AddRange(permissionCodes.Select(code => new Claim("permission", code)));

        var key = new SymmetricSecurityKey(Convert.FromBase64String(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(AccessTokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
