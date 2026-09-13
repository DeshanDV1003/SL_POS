using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Catalog;
using UniversalPOS.Application.Crm;
using UniversalPOS.Application.Identity;
using UniversalPOS.Application.Organization;
using UniversalPOS.Application.Purchasing;

namespace UniversalPOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationQueryService, OrganizationQueryService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ICustomerService, CustomerService>();
        return services;
    }
}
