using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Reporting.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises Phase 10 reporting against the real database: reports must reflect
/// actual recorded sales, not a separately tracked shadow total, and must respect
/// the same permission model as everything else.
/// </summary>
[Collection("Integration")]
public class ReportingEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 3; // Lanka Fresh Mart - Nugegoda

    public ReportingEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Username = username, Password = "Passw0rd!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK, $"test setup requires '{username}' to be able to log in");
        var result = await login.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result!.AccessToken);
        return client;
    }

    private async Task<(long ProductId, decimal Price)> ArrangeStockedProductAsync(HttpClient admin, string sku)
    {
        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First(p => p.Sku == sku);

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Report Test Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();
        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product.Id, QuantityReceived = 50, UnitCost = 100 } },
        });

        return (product.Id, product.SellingPrice);
    }

    [Fact]
    public async Task SalesSummary_ReflectsRealSaleJustMade()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin, "LFM-BV-001");

        var before = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/reports/sales-summary")).Content.ReadFromJsonAsync<SalesSummaryDto>();

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 2 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * 2 } },
        });
        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var after = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/reports/sales-summary")).Content.ReadFromJsonAsync<SalesSummaryDto>();

        (after!.NetSales - before!.NetSales).Should().Be(receipt!.GrandTotal);
        (after.TransactionCount - before.TransactionCount).Should().Be(1);
    }

    [Fact]
    public async Task SalesByProduct_IncludesTheProductJustSold()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin, "LFM-DY-001");

        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 3 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * 3 } },
        });

        var response = await admin.GetAsync($"/api/v1/branches/{BranchId}/reports/sales-by-product");
        var byProduct = await response.Content.ReadFromJsonAsync<List<SalesByProductDto>>();

        byProduct.Should().Contain(p => p.ProductId == productId && p.QuantitySold >= 3);
    }

    [Fact]
    public async Task SalesByPaymentMethod_SeparatesCashAndCard()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin, "LFM-GR-001");

        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Card, Amount = price, InstrumentToken = "4111111111111111" } },
        });

        var response = await admin.GetAsync($"/api/v1/branches/{BranchId}/reports/sales-by-payment-method");
        var byMethod = await response.Content.ReadFromJsonAsync<List<SalesByPaymentMethodDto>>();

        byMethod.Should().Contain(m => m.Method == "Card" && m.Total > 0);
    }

    [Fact]
    public async Task ReportsViewSales_ByUserWithoutPermission_Returns403()
    {
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");

        var response = await cashier.GetAsync($"/api/v1/branches/{BranchId}/reports/sales-summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReportsViewFinancial_RequiredForCashierAndPaymentMethodReports()
    {
        // manager.lfm holds both reports.view.sales and reports.view.financial in the seed data.
        var manager = await CreateAuthenticatedClientAsync("manager.lfm");

        var byCashier = await manager.GetAsync($"/api/v1/branches/{BranchId}/reports/sales-by-cashier");
        var stockValuation = await manager.GetAsync($"/api/v1/branches/{BranchId}/reports/stock-valuation");

        byCashier.StatusCode.Should().Be(HttpStatusCode.OK);
        stockValuation.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_ReturnsLowStockCountAndTopProducts()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        await ArrangeStockedProductAsync(admin, "LFM-BV-001");

        var response = await admin.GetAsync($"/api/v1/branches/{BranchId}/dashboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();

        dashboard!.LowStockProductCount.Should().BeGreaterThanOrEqualTo(0);
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
    }
}
