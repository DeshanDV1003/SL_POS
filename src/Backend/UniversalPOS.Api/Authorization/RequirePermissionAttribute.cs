using Microsoft.AspNetCore.Authorization;

namespace UniversalPOS.Api.Authorization;

public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionCode) : base(permissionCode)
    {
    }
}
