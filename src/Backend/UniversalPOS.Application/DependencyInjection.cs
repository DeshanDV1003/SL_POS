using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Identity;
using UniversalPOS.Application.Organization;

namespace UniversalPOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationQueryService, OrganizationQueryService>();
        return services;
    }
}
