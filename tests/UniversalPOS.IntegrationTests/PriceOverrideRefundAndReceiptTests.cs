using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the Phase 6 gap-fill features against the real database: price override
/// (permission-gated), refunds as a distinct document from void (partial refunds,
/// over-refund rejection, payment-total matching), and receipt/tax-invoice rendering.
/// </summary>
[Collection("Integration")]
public class PriceOverrideRefundAndReceiptTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 3; // Lanka Fresh Mart - Nugegoda

    public PriceOverrideRefundAndReceiptTests(CustomWebApplicationFactory factory)
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

    private async Task<(long ProductId, decimal Price)> ArrangeStockedProductAsync(HttpClient admin)
    {
        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First(p => p.Sku == "LFM-BV-001");

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Gap6 Test Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();
        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product.Id, QuantityReceived = 50, UnitCost = 100 } },
        });

        return (product.Id, product.SellingPrice);
    }

    [Fact]
    public async Task Checkout_PriceOverride_ByUserWithoutPermission_Returns403()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, _) = await ArrangeStockedProductAsync(admin);
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");

        var response = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1, UnitPriceOverride = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Checkout_PriceOverride_ByAuthorizedUser_UsesOverriddenPrice()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, normalPrice) = await ArrangeStockedProductAsync(admin);
        var overridePrice = normalPrice - 100;

        var response = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1, UnitPriceOverride = overridePrice } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = overridePrice } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = await response.Content.ReadFromJsonAsync<SaleReceiptDto>();
        receipt!.Lines[0].UnitPrice.Should().Be(overridePrice);
        receipt.GrandTotal.Should().Be(overridePrice);
    }

    [Fact]
    public async Task Refund_PartialQuantity_RestoresStockWithoutMutatingOriginalSale()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 3 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * 3 } },
        });
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var stockResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/inventory/stock-on-hand");
        var stockBeforeRefund = (await stockResponse.Content.ReadFromJsonAsync<List<Application.Inventory.Dtos.StockOnHandDto>>())!
            .First(s => s.ProductId == productId).QuantityOnHand;

        var refundResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales/{sale!.Id}/refund", new RefundSaleRequest
        {
            Reason = "Customer returned 1 unit",
            Lines = new List<RefundLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        refundResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var refund = await refundResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();
        refund!.Status.Should().Be("Refunded");
        refund.GrandTotal.Should().Be(price);

        var stockAfterResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/inventory/stock-on-hand");
        var stockAfterRefund = (await stockAfterResponse.Content.ReadFromJsonAsync<List<Application.Inventory.Dtos.StockOnHandDto>>())!
            .First(s => s.ProductId == productId).QuantityOnHand;
        (stockAfterRefund - stockBeforeRefund).Should().Be(1);

        var originalAfter = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/sales/{sale.Id}")).Content.ReadFromJsonAsync<SaleReceiptDto>();
        originalAfter!.Status.Should().Be("Completed", "the original sale is never mutated by a refund");
    }

    [Fact]
    public async Task Refund_MoreThanRemainingQuantity_Returns409()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var response = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales/{sale!.Id}/refund", new RefundSaleRequest
        {
            Reason = "over-refund attempt",
            Lines = new List<RefundLineRequest> { new() { ProductId = productId, Quantity = 5 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * 5 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Refund_PaymentTotalMismatch_Returns400()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var response = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales/{sale!.Id}/refund", new RefundSaleRequest
        {
            Reason = "mismatch test",
            Lines = new List<RefundLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refund_ByUserWithoutPermission_Returns403()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var response = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales/{sale!.Id}/refund", new RefundSaleRequest
        {
            Reason = "test",
            Lines = new List<RefundLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Receipt_SimplifiedMode_ContainsInvoiceNumberAndTotal()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var receiptResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/sales/{sale!.Id}/receipt");
        receiptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = await receiptResponse.Content.ReadAsStringAsync();

        text.Should().Contain(sale.InvoiceNumber!);
        text.Should().Contain("TOTAL");
        text.Should().NotContain("TAX INVOICE");
    }

    [Fact]
    public async Task Receipt_FullTaxInvoiceMode_ContainsMandatedFields()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price) = await ArrangeStockedProductAsync(admin);

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = 3,
            InvoiceMode = InvoiceMode.FullTaxInvoice,
            PurchaserTin = "999888777",
            PurchaserName = "Test Purchaser Ltd",
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var text = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/sales/{sale!.Id}/receipt")).Content.ReadAsStringAsync();

        text.Should().Contain("TAX INVOICE");
        text.Should().Contain("999888777");
        text.Should().Contain("Test Purchaser Ltd");
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
    }
}
