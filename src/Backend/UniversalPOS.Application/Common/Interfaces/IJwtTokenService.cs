using UniversalPOS.Domain.Identity;

namespace UniversalPOS.Application.Common.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Short-lived access token embedding user, company, branch, and permission claims.</summary>
    string GenerateAccessToken(AppUser user, IReadOnlyCollection<string> permissionCodes, long? terminalId);

    TimeSpan AccessTokenLifetime { get; }
}
