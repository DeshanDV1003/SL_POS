using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;

namespace UniversalPOS.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private System.Security.Claims.ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public long? UserId
    {
        get
        {
            var sub = User?.FindFirst("sub")?.Value;
            return long.TryParse(sub, out var id) ? id : null;
        }
    }

    public long CompanyId
    {
        get
        {
            var claim = User?.FindFirst("company_id")?.Value;
            if (!long.TryParse(claim, out var id))
            {
                throw new ForbiddenException("No authenticated company context.");
            }
            return id;
        }
    }

    public long? TerminalId
    {
        get
        {
            var claim = User?.FindFirst("terminal_id")?.Value;
            return long.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool HasPermission(string code) => User?.Claims.Any(c => c.Type == "permission" && c.Value == code) ?? false;
}
