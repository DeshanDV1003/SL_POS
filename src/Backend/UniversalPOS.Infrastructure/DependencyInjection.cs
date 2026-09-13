using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Sales;
using UniversalPOS.Infrastructure.Payments;
using UniversalPOS.Infrastructure.Persistence;
using UniversalPOS.Infrastructure.Services;

namespace UniversalPOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IFiscalReportingProvider, NullFiscalReportingProvider>();

        services.AddScoped<IPaymentProvider, CashPaymentProvider>();
        services.AddScoped<IPaymentProvider, SandboxCardPaymentProvider>();

        return services;
    }
}
