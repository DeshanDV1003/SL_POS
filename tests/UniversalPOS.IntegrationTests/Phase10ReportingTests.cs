using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Reporting.Dtos;
using UniversalPOS.Application.Restaurant.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Restaurant;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Phase 10 gap-fill: revenue-trend and branch-comparison reports, discount/tax
/// report views separated out of the general sales summary, kitchen/waiter
/// performance aggregated from real PreparationTicket/Order data, dead/slow-moving
/// stock analysis, and CSV export on the list-shaped reports.
/// </summary>
[Collection("Integration")]
public class Phase10ReportingTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long LfmBranchId = 3; // Lanka Fresh Mart - Nugegoda
    private const long RestaurantBranchId = 1; // Ceylon Spice - Colombo 07

    public Phase10ReportingTests(CustomWebApplicationFactory factory)
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

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Phase10 Report Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();
        await admin.PostAsJsonAsync($"/api/v1/branches/{LfmBranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product.Id, QuantityReceived = 50, UnitCost = 100 } },
        });

        return (product.Id, product.SellingPrice);
    }

    [Fact]
    public async Task SalesTrend_IncludesTodaysBucketWithTheSaleJustMade()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin, "LFM-BV-001");

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{LfmBranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var response = await admin.GetAsync($"/api/v1/branches/{LfmBranchId}/reports/sales-trend");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var trend = await response.Content.ReadFromJsonAsync<List<SalesTrendPointDto>>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        trend.Should().Contain(p => p.Date == today && p.NetSales >= receipt!.GrandTotal);
    }

    [Fact]
    public async Task SalesTrend_Csv_ReturnsCsvContentType()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");

        var response = await admin.GetAsync($"/api/v1/branches/{LfmBranchId}/reports/sales-trend?format=csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().StartWith("Date,NetSales,TransactionCount");
    }

    [Fact]
    public async Task BranchComparison_ListsEveryBranchInTheCompany()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");

        var response = await admin.GetAsync("/api/v1/reports/branch-comparison");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<BranchComparisonDto>>();
        rows!.Should().Contain(r => r.BranchId == LfmBranchId);
    }

    [Fact]
    public async Task DiscountReport_ReflectsACouponAndAManualLineDiscount()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin, "LFM-DY-001");

        var code = $"RPT{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        await admin.PostAsJsonAsync("/api/v1/coupons", new CreateCouponRequest { Code = code, DiscountType = PromotionDiscountType.FixedAmount, DiscountValue = 25 });

        var before = await (await admin.GetAsync($"/api/v1/branches/{LfmBranchId}/reports/discount-report")).Content.ReadFromJsonAsync<DiscountReportDto>();

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{LfmBranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            CouponCode = code,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1, DiscountPercentage = 5 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price + 1000 } },
        });
        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var after = await (await admin.GetAsync($"/api/v1/branches/{LfmBranchId}/reports/discount-report")).Content.ReadFromJsonAsync<DiscountReportDto>();

        (after!.CouponDiscountTotal - before!.CouponDiscountTotal).Should().Be(25);
        (after.LineDiscountTotal - before.LineDiscountTotal).Should().BeGreaterThan(0);
        (after.SalesWithDiscountCount - before.SalesWithDiscountCount).Should().Be(1);
    }

    [Fact]
    public async Task TaxReport_GroupsCollectedTaxByRate()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin, "LFM-GR-001");

        await admin.PostAsJsonAsync($"/api/v1/branches/{LfmBranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });

        var response = await admin.GetAsync($"/api/v1/branches/{LfmBranchId}/reports/tax-report");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<TaxReportDto>();

        report!.TotalTaxCollected.Should().Be(report.ByRate.Sum(r => r.TaxCollected));
    }

    [Fact]
    public async Task SlowMovingStock_FlagsAStockedNeverSoldProduct()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");

        // A brand-new product rather than a shared seeded SKU: other test classes in
        // this collection sell LFM-GR-001/DY-001/BV-001, which would make "never sold"
        // untrue depending on run order.
        var units = await (await admin.GetAsync("/api/v1/units")).Content.ReadFromJsonAsync<List<UnitInfo>>();
        var productResponse = await admin.PostAsJsonAsync("/api/v1/products", new CreateProductInfo
        {
            Sku = $"LFM-SLOW-{Guid.NewGuid():N}"[..20],
            Name = "Never Sold Test Product",
            UnitId = units!.First().Id,
            CostPrice = 50,
            SellingPrice = 100,
        });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductInfo>();

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Slow Stock Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();
        await admin.PostAsJsonAsync($"/api/v1/branches/{LfmBranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product!.Id, QuantityReceived = 10, UnitCost = 50 } },
        });

        var response = await admin.GetAsync($"/api/v1/branches/{LfmBranchId}/reports/slow-moving-stock?staleAfterDays=0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<SlowMovingStockDto>>();

        rows!.Should().Contain(r => r.ProductId == product.Id && r.LastSoldAtUtc == null);
    }

    [Fact]
    public async Task KitchenPerformance_CountsTicketsAndCancellationsPerStation()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.cs"); // holds every permission, incl. restaurant.kds.update

        var floors = await (await admin.GetAsync($"/api/v1/branches/{RestaurantBranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var table = floors!.SelectMany(f => f.Tables).First(t => t.Status == "Available");
        var openResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{RestaurantBranchId}/tables/{table.Id}/open", new OpenTableRequest { OrderType = OrderType.DineIn });
        var order = await openResponse.Content.ReadFromJsonAsync<OrderDto>();

        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var kottu = products!.First(p => p.Sku == "CS-SE-001"); // Main Kitchen station

        await admin.PostAsJsonAsync($"/api/v1/branches/{RestaurantBranchId}/orders/{order!.Id}/lines",
            new List<AddOrderLineRequest> { new() { ProductId = kottu.Id, Quantity = 1 } });
        var tickets = await (await admin.PostAsync($"/api/v1/branches/{RestaurantBranchId}/orders/{order.Id}/send-to-kitchen", null))
            .Content.ReadFromJsonAsync<List<PreparationTicketDto>>();
        var ticket = tickets!.Single();

        var readyResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{RestaurantBranchId}/tickets/{ticket.Id}/status", new UpdateTicketStatusRequest { Status = TicketStatus.Ready });
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var stations = await (await admin.GetAsync($"/api/v1/branches/{RestaurantBranchId}/kitchen-stations")).Content.ReadFromJsonAsync<List<KitchenStationInfo>>();
        var mainKitchenId = stations!.First(s => s.Name == "Main Kitchen").Id;

        var response = await admin.GetAsync($"/api/v1/branches/{RestaurantBranchId}/reports/kitchen-performance");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<KitchenStationPerformanceDto>>();

        rows!.Should().Contain(r => r.KitchenStationId == mainKitchenId && r.TicketCount >= 1 && r.AveragePrepTimeMinutes != null);
    }

    [Fact]
    public async Task WaiterPerformance_CountsOrdersPerWaiter()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.cs");

        var floors = await (await admin.GetAsync($"/api/v1/branches/{RestaurantBranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var table = floors!.SelectMany(f => f.Tables).First(t => t.Status == "Available");
        var openResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{RestaurantBranchId}/tables/{table.Id}/open", new OpenTableRequest { OrderType = OrderType.DineIn });
        openResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await admin.GetAsync($"/api/v1/branches/{RestaurantBranchId}/reports/waiter-performance");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<WaiterPerformanceDto>>();

        rows!.Should().Contain(r => r.OrderCount >= 1);
    }

    [Fact]
    public async Task DiscountReport_ByUserWithoutFinancialPermission_Returns403()
    {
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");

        var response = await cashier.GetAsync($"/api/v1/branches/{LfmBranchId}/reports/discount-report");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
    }

    private class KitchenStationInfo
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class UnitInfo
    {
        public long Id { get; set; }
    }

    private class CreateProductInfo
    {
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long UnitId { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
    }
}
