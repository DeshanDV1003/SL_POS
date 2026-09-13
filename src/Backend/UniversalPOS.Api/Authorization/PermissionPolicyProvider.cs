using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace UniversalPOS.Api.Authorization;

/// <summary>
/// Treats any policy name not already registered as a permission code and builds a
/// PermissionRequirement for it on demand, so a new PermissionCodes.* constant never
/// needs a matching policy registration in Program.cs.
/// </summary>
public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existing = await base.GetPolicyAsync(policyName);
        if (existing is not null)
        {
            return existing;
        }

        return new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
