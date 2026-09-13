using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Cash;
using UniversalPOS.Application.Catalog;
using UniversalPOS.Application.Crm;
using UniversalPOS.Application.Identity;
using UniversalPOS.Application.Inventory;
using UniversalPOS.Application.Organization;
using UniversalPOS.Application.Purchasing;
using UniversalPOS.Application.Reporting;
using UniversalPOS.Application.Restaurant;
using UniversalPOS.Application.Sales;

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
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IPromotionEngine, PromotionEngine>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IReceiptRenderer, ReceiptRenderer>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<ICashService, CashService>();
        services.AddScoped<IReportingService, ReportingService>();
        return services;
    }
}
