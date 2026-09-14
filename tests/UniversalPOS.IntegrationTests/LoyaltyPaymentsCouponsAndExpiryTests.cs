using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Crm.Dtos;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Organization.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Crm;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Phase 9 gap-fill: pay-with-points at checkout, customer-entered coupon codes,
/// per-company-configurable loyalty earn/redemption rates, and the points-expiry job.
/// Uses Lanka Fresh Mart's Nugegoda branch (id 3, company 2) and always restores
/// company-wide loyalty settings afterwards since they're shared with other test
/// classes in this collection.
/// </summary>
[Collection("Integration")]
public class LoyaltyPaymentsCouponsAndExpiryTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 3;
    private const long CompanyId = 2;

    public LoyaltyPaymentsCouponsAndExpiryTests(CustomWebApplicationFactory factory)
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

    private static async Task RestoreDefaultLoyaltySettingsAsync(HttpClient admin)
    {
        await admin.PutAsJsonAsync($"/api/v1/companies/{CompanyId}/loyalty-settings",
            new UpdateCompanyLoyaltySettingsRequest { LoyaltyPointsPerCurrencyUnit = 0.01m, LoyaltyPointRedemptionValue = 1.00m, LoyaltyPointsExpiryMonths = null });
    }

    private async Task<(long ProductId, decimal Price)> ArrangeStockedProductAsync(HttpClient admin)
    {
        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First(p => p.Sku == "LFM-BV-001");

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Loyalty Test Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();
        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product.Id, QuantityReceived = 100, UnitCost = 100 } },
        });

        return (product.Id, product.SellingPrice);
    }

    [Fact]
    public async Task Checkout_PayingPartlyWithPoints_DeductsBalanceAndRecordsRedeemedTransaction()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var customerResponse = await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest { Name = $"Points Payer {Guid.NewGuid():N}" });
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();
        await admin.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/loyalty/adjust", new AdjustLoyaltyPointsRequest { PointsChange = 100, Reason = "Test seed" });

        // Default redemption value is LKR 1.00/point, so 30 in points = 30 points.
        var pointsPortion = 30m;
        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            CustomerId = customer.Id,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest>
            {
                new() { Method = PaymentMethod.LoyaltyPoints, Amount = pointsPortion },
                new() { Method = PaymentMethod.Cash, Amount = price - pointsPortion },
            },
        });

        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();
        receipt!.Payments.Should().ContainSingle(p => p.Method == "LoyaltyPoints" && p.Amount == pointsPortion && p.ProviderReference == "LOYALTY");

        var history = await (await admin.GetAsync($"/api/v1/customers/{customer.Id}/loyalty/history")).Content.ReadFromJsonAsync<List<LoyaltyTransactionDto>>();
        history!.Should().ContainSingle(h => h.TransactionType == "Redeemed" && h.PointsChange == -30);

        var customersAfter = await (await admin.GetAsync("/api/v1/customers")).Content.ReadFromJsonAsync<List<CustomerDto>>();
        var updated = customersAfter!.First(c => c.Id == customer.Id);
        // 100 - 30 redeemed + whatever was earned on the (points-reduced) grand total.
        updated.LoyaltyPointsBalance.Should().BeGreaterThanOrEqualTo(70);
    }

    [Fact]
    public async Task Checkout_PayingWithPoints_WithoutCustomer_Returns400()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.LoyaltyPoints, Amount = price } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Checkout_PayingWithPoints_ExceedingBalance_Returns409()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var customerResponse = await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest { Name = $"Zero Points Customer {Guid.NewGuid():N}" });
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var response = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            CustomerId = customer!.Id,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest>
            {
                new() { Method = PaymentMethod.LoyaltyPoints, Amount = 1 },
                new() { Method = PaymentMethod.Cash, Amount = price - 1 },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Checkout_WithValidCoupon_ReducesGrandTotalAndIncrementsRedemptionCount()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var code = $"TEST{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var couponResponse = await admin.PostAsJsonAsync("/api/v1/coupons", new CreateCouponRequest
        {
            Code = code,
            DiscountType = PromotionDiscountType.Percentage,
            DiscountValue = 10,
        });
        couponResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Cash may overpay (change is issued), so tender comfortably above the
        // undiscounted price and verify the coupon-reduced total from the receipt.
        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            CouponCode = code.ToLowerInvariant(), // case-insensitive
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price + 1000 } },
        });

        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();
        receipt!.CouponDiscountAmount.Should().BeGreaterThan(0);

        // GrandTotal already has the coupon discount folded in, so adding it back
        // recovers the pre-coupon total without needing to reconstruct it from
        // SubTotal/TaxTotal (which don't sum linearly for VAT-inclusive lines, since
        // TaxTotal there is the informational embedded-tax portion, not additive).
        var preCouponTotal = receipt.GrandTotal + receipt.CouponDiscountAmount;
        receipt.CouponDiscountAmount.Should().BeApproximately(Math.Round(preCouponTotal * 0.10m, 2), 0.02m);
        receipt.ChangeDue.Should().Be(price + 1000 - receipt.GrandTotal);

        var coupons = await (await admin.GetAsync("/api/v1/coupons")).Content.ReadFromJsonAsync<List<CouponDto>>();
        coupons!.First(c => c.Code == code).TimesRedeemed.Should().Be(1);
    }

    [Fact]
    public async Task Checkout_WithUnknownCouponCode_Returns404()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            CouponCode = $"NOSUCHCODE{Guid.NewGuid():N}",
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateCoupon_DuplicateCode_Returns409()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var code = $"DUP{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var first = await admin.PostAsJsonAsync("/api/v1/coupons", new CreateCouponRequest { Code = code, DiscountType = PromotionDiscountType.FixedAmount, DiscountValue = 50 });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await admin.PostAsJsonAsync("/api/v1/coupons", new CreateCouponRequest { Code = code, DiscountType = PromotionDiscountType.FixedAmount, DiscountValue = 50 });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateLoyaltySettings_ByManagerWithoutCompanyManage_Returns403()
    {
        var manager = await CreateAuthenticatedClientAsync("manager.lfm");

        var response = await manager.PutAsJsonAsync($"/api/v1/companies/{CompanyId}/loyalty-settings",
            new UpdateCompanyLoyaltySettingsRequest { LoyaltyPointsPerCurrencyUnit = 0.05m, LoyaltyPointRedemptionValue = 1, LoyaltyPointsExpiryMonths = 12 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateLoyaltySettings_ChangesActualEarnRateAppliedAtCheckout()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        try
        {
            var (productId, price) = await ArrangeStockedProductAsync(admin);

            // A much higher earn rate (1 point per LKR 1, instead of the default 1 per LKR 100).
            var updateResponse = await admin.PutAsJsonAsync($"/api/v1/companies/{CompanyId}/loyalty-settings",
                new UpdateCompanyLoyaltySettingsRequest { LoyaltyPointsPerCurrencyUnit = 1m, LoyaltyPointRedemptionValue = 1m, LoyaltyPointsExpiryMonths = null });
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var customerResponse = await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest { Name = $"Earn Rate Customer {Guid.NewGuid():N}" });
            var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

            var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
            {
                TerminalId = 3,
                CustomerId = customer!.Id,
                Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
                Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
            });
            saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

            var customersAfter = await (await admin.GetAsync("/api/v1/customers")).Content.ReadFromJsonAsync<List<CustomerDto>>();
            var updated = customersAfter!.First(c => c.Id == customer.Id);
            // At 1 point per currency unit, earned points should equal floor(GrandTotal) exactly.
            updated.LoyaltyPointsBalance.Should().Be((int)Math.Floor(receipt!.GrandTotal));
        }
        finally
        {
            await RestoreDefaultLoyaltySettingsAsync(admin);
        }
    }

    [Fact]
    public async Task ExpirePoints_WithNoDueBatches_AffectsNoCustomers()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");

        var response = await admin.PostAsync($"/api/v1/companies/{CompanyId}/loyalty/expire-points", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ExpireResultDto>();
        body!.CustomersAffected.Should().Be(0);
    }

    [Fact]
    public async Task ExpirePoints_WithABackdatedEarnedBatch_DeductsBalanceAndIsIdempotent()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var customerResponse = await admin.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest { Name = $"Expiry Test Customer {Guid.NewGuid():N}" });
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        await admin.PostAsJsonAsync($"/api/v1/customers/{customer!.Id}/loyalty/adjust", new AdjustLoyaltyPointsRequest { PointsChange = 200, Reason = "Seed for expiry test" });

        // Simulate an Earned batch whose expiry date has already passed — there is no
        // API to earn points into the past, so this reaches into the real database the
        // same way an end-to-end verification against a live instance would, via the
        // host's own DI container rather than a second connection.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            db.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                CompanyId = CompanyId,
                CustomerId = customer.Id,
                TransactionType = LoyaltyTransactionType.Earned,
                PointsChange = 40,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-7),
            });
            // Reflect the extra 40 points in the running balance too, exactly as EarnPointsForSaleAsync would have.
            var customerEntity = await db.Customers.FirstAsync(c => c.Id == customer.Id);
            customerEntity.LoyaltyPointsBalance += 40;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var firstRun = await admin.PostAsync($"/api/v1/companies/{CompanyId}/loyalty/expire-points", null);
        firstRun.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await firstRun.Content.ReadFromJsonAsync<ExpireResultDto>();
        firstBody!.CustomersAffected.Should().Be(1);

        var afterFirstRun = await (await admin.GetAsync("/api/v1/customers")).Content.ReadFromJsonAsync<List<CustomerDto>>();
        afterFirstRun!.First(c => c.Id == customer.Id).LoyaltyPointsBalance.Should().Be(200); // 240 - 40 expired

        var history = await (await admin.GetAsync($"/api/v1/customers/{customer.Id}/loyalty/history")).Content.ReadFromJsonAsync<List<LoyaltyTransactionDto>>();
        history!.Should().ContainSingle(h => h.TransactionType == "Expired" && h.PointsChange == -40);

        // Idempotent: running it again must not expire the same batch twice.
        var secondRun = await admin.PostAsync($"/api/v1/companies/{CompanyId}/loyalty/expire-points", null);
        var secondBody = await secondRun.Content.ReadFromJsonAsync<ExpireResultDto>();
        secondBody!.CustomersAffected.Should().Be(0);
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
    }

    private class ExpireResultDto
    {
        public int CustomersAffected { get; set; }
    }
}
