using Microsoft.AspNetCore.Authorization;

namespace UniversalPOS.Api.Authorization;

/// <summary>
/// Checks the "permission" claims embedded in the access token (resolved server-side at
/// login from the user's role-permission set) against the code a controller action
/// requires. Business logic never checks role names directly — only permission codes.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Claims.Any(c => c.Type == "permission" && c.Value == requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
