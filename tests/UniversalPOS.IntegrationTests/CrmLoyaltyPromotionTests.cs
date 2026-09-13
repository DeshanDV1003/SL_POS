using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Crm.Dtos;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the Phase 9 CRM/loyalty/promotions flow against the real database: a
/// sale attached to a customer earns real, traceable loyalty points and can trigger a
/// membership tier upgrade; a manual point adjustment writes a real audit log entry;
/// and an automatic promotion correctly out-competes a smaller manual line discount.
/// </summary>
[Collection("Integration")]
public class CrmLoyaltyPromotionTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 3; // Lanka Fresh Mart - Nugegoda

    public CrmLoyaltyPromotionTests(CustomWebApplicationFactory factory)
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

    private async Task<(long ProductId, decimal Price, long CategoryId)> ArrangeStockedProductAsync(HttpClient admin, string sku)
    {
        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First(p => p.Sku == sku);

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"CRM Test Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();
        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product.Id, QuantityReceived = 100, UnitCost = 100 } },
        });

        return (product.Id, product.SellingPrice, product.CategoryId!.Value);
    }

    [Fact]
    public async Task Sale_WithCustomer_EarnsLoyaltyPointsAndUpgradesTier()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, _) = await ArrangeStockedProductAsync(admin, "LFM-BV-001");

        var tierResponse = await admin.PostAsJsonAsync("/api/v1/membership-tiers", new CreateMembershipTierRequest { Name = $"Gold-{Guid.NewGuid():N}", MinimumPoints = 1, PointsMultiplier = 1 });
        tierResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tier = await tierResponse.Content.ReadFromJsonAsync<MembershipTierDto>();

        var customerResponse = await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest { Name = $"Loyalty Test Customer {Guid.NewGuid():N}" });
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        // 10 units at `price` should earn at least 1 point (rate is 1 point per LKR 100), enough to cross MinimumPoints=1.
        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            CustomerId = customer!.Id,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 10 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * 10 } },
        });
        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var customersAfter = await (await admin.GetAsync("/api/v1/customers")).Content.ReadFromJsonAsync<List<CustomerDto>>();
        var updated = customersAfter!.First(c => c.Id == customer.Id);
        updated.LoyaltyPointsBalance.Should().BeGreaterThan(0);
        updated.MembershipTierId.Should().Be(tier!.Id);

        var historyResponse = await admin.GetAsync($"/api/v1/customers/{customer.Id}/loyalty/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<LoyaltyTransactionDto>>();
        history.Should().ContainSingle(h => h.TransactionType == "Earned" && h.PointsChange == updated.LoyaltyPointsBalance);
    }

    [Fact]
    public async Task RedeemPoints_MoreThanBalance_Returns409()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var customerResponse = await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest { Name = $"Redeem Test Customer {Guid.NewGuid():N}" });
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var response = await admin.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/loyalty/redeem", new RedeemPointsRequest { Points = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AdjustPoints_ByUserWithoutPermission_Returns403()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");
        var customerResponse = await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest { Name = $"Adjust Test Customer {Guid.NewGuid():N}" });
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var response = await cashier.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/loyalty/adjust", new AdjustLoyaltyPointsRequest { PointsChange = 10, Reason = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdjustPoints_ByAuthorizedUser_UpdatesBalanceAndRecordsHistory()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var customerResponse = await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest { Name = $"Adjust Success Customer {Guid.NewGuid():N}" });
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var response = await admin.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/loyalty/adjust", new AdjustLoyaltyPointsRequest { PointsChange = 25, Reason = "Test goodwill credit" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var customersAfter = await (await admin.GetAsync("/api/v1/customers")).Content.ReadFromJsonAsync<List<CustomerDto>>();
        customersAfter!.First(c => c.Id == customer.Id).LoyaltyPointsBalance.Should().Be(25);

        var history = await (await admin.GetAsync($"/api/v1/customers/{customer.Id}/loyalty/history")).Content.ReadFromJsonAsync<List<LoyaltyTransactionDto>>();
        history.Should().ContainSingle(h => h.TransactionType == "ManualAdjustment" && h.PointsChange == 25);
    }

    [Fact]
    public async Task Checkout_WithActivePromotion_OverridesSmallerManualDiscount()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, categoryId) = await ArrangeStockedProductAsync(admin, "LFM-DY-001");

        var promoResponse = await admin.PostAsJsonAsync("/api/v1/promotions", new CreatePromotionRequest
        {
            Name = $"Test Promo {Guid.NewGuid():N}",
            DiscountType = PromotionDiscountType.Percentage,
            DiscountValue = 15,
            Scope = PromotionScope.Category,
            CategoryId = categoryId,
            MinQuantity = 1,
            StartDateUtc = DateTime.UtcNow.AddDays(-1),
        });
        promoResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1, DiscountPercentage = 2 } }, // manual 2% is smaller than the 15% promo
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });

        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();
        receipt!.Lines[0].DiscountPercentage.Should().Be(15, "the active 15% category promotion should beat the 2% manual discount");
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
        public long? CategoryId { get; set; }
    }
}
