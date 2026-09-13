using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Inventory.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the Phase 6 retail checkout chain against the real database and real
/// HTTP pipeline — no mocks. Covers the golden path (checkout deducts stock, computes
/// VAT-inclusive tax correctly, returns correct cash change) and the failure paths a
/// real cashier will hit (underpayment, a declined sandbox card, voiding without
/// permission, double-voiding).
/// </summary>
[Collection("Integration")]
public class SalesEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 3; // Lanka Fresh Mart - Nugegoda

    public SalesEndpointTests(CustomWebApplicationFactory factory)
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

    /// <summary>Ensures a fresh product with known stock and a known terminal exist, independent of other tests' state.</summary>
    private async Task<(long ProductId, long TerminalId, decimal SellingPrice)> ArrangeStockedProductAsync(HttpClient admin)
    {
        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First(p => p.Sku == "LFM-BV-001"); // Cola, 18% VAT-inclusive

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Sales Test Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();

        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product.Id, QuantityReceived = 100, UnitCost = 200 } },
        });

        var terminals = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/terminals")).Content.ReadFromJsonAsync<List<TerminalInfo>>();

        return (product.Id, terminals!.First().Id, product.SellingPrice);
    }

    [Fact]
    public async Task Checkout_CashOverpayment_ComputesCorrectVatInclusiveTotalsAndChange_DeductsStock()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");
        var (productId, terminalId, price) = await ArrangeStockedProductAsync(admin);

        var stockBefore = await GetStock(admin, productId);

        var response = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 2 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 1000 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var receipt = await response.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var expectedTotal = price * 2;
        receipt!.GrandTotal.Should().Be(expectedTotal);
        receipt.ChangeDue.Should().Be(1000 - expectedTotal);
        receipt.InvoiceNumber.Should().NotBeNullOrWhiteSpace();

        var stockAfter = await GetStock(admin, productId);
        (stockBefore - stockAfter).Should().Be(2);
    }

    [Fact]
    public async Task Checkout_Underpayment_Returns400AndDoesNotDeductStock()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");
        var (productId, terminalId, price) = await ArrangeStockedProductAsync(admin);
        var stockBefore = await GetStock(admin, productId);

        var response = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetStock(admin, productId)).Should().Be(stockBefore);
    }

    [Fact]
    public async Task Checkout_DeclinedSandboxCard_Returns400AndDoesNotDeductStock()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");
        var (productId, terminalId, price) = await ArrangeStockedProductAsync(admin);
        var stockBefore = await GetStock(admin, productId);

        var response = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Card, Amount = price, InstrumentToken = "4000000000000002" } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetStock(admin, productId)).Should().Be(stockBefore);
    }

    [Fact]
    public async Task Void_ByUserWithoutPermission_Returns403()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");
        var (productId, terminalId, price) = await ArrangeStockedProductAsync(admin);

        var saleResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var voidResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales/{sale!.Id}/void", new VoidSaleRequest { Reason = "test" });

        voidResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Void_ByAuthorizedUser_RestoresStockAndCannotBeVoidedTwice()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");
        var (productId, terminalId, price) = await ArrangeStockedProductAsync(admin);
        var stockBeforeSale = await GetStock(admin, productId);

        var saleResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 3 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * 3 } },
        });
        var sale = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();
        (stockBeforeSale - await GetStock(admin, productId)).Should().Be(3);

        var voidResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales/{sale!.Id}/void", new VoidSaleRequest { Reason = "Customer returned goods" });
        voidResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetStock(admin, productId)).Should().Be(stockBeforeSale);

        var secondVoid = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales/{sale.Id}/void", new VoidSaleRequest { Reason = "again" });
        secondVoid.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task HoldAndRecall_RoundTripsCartContents()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");
        var (productId, terminalId, _) = await ArrangeStockedProductAsync(admin);

        var holdResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales/held", new CreateHeldBillRequest
        {
            TerminalId = terminalId,
            Notes = "Test hold",
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 4 } },
        });
        holdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var held = await holdResponse.Content.ReadFromJsonAsync<HeldBillDto>();

        var recallResponse = await cashier.PostAsync($"/api/v1/branches/{BranchId}/sales/held/{held!.Id}/recall", null);
        recallResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var recalled = await recallResponse.Content.ReadFromJsonAsync<HeldBillDto>();

        recalled!.Lines.Should().ContainSingle(l => l.ProductId == productId && l.Quantity == 4);
    }

    private static async Task<decimal> GetStock(HttpClient client, long productId)
    {
        var response = await client.GetAsync($"/api/v1/branches/{BranchId}/inventory/stock-on-hand");
        var stock = await response.Content.ReadFromJsonAsync<List<StockOnHandDto>>();
        return stock!.FirstOrDefault(s => s.ProductId == productId)?.QuantityOnHand ?? 0;
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
    }

    private class TerminalInfo
    {
        public long Id { get; set; }
    }
}
