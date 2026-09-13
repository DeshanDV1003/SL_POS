using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Inventory.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the Phase 5 gap-fill features against the real database: branch-to-
/// branch stock transfer, physical stock count reconciliation, and supplier
/// invoice/payment tracking. Uses Ceylon Spice's two branches (Colombo=1, Kandy=2)
/// since transfers need two branches under the same company.
/// </summary>
[Collection("Integration")]
public class StockTransferCountAndSupplierBillingTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long ColomboBranchId = 1;
    private const long KandyBranchId = 2;

    public StockTransferCountAndSupplierBillingTests(CustomWebApplicationFactory factory)
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

    private async Task<(long ProductId, SupplierDto Supplier)> ArrangeStockedProductAtColomboAsync(HttpClient admin, decimal quantity)
    {
        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First(p => p.Sku == "CS-BV-001"); // King Coconut, Ceylon Spice

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Gap Test Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();

        await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product.Id, QuantityReceived = quantity, UnitCost = 80 } },
        });

        return (product.Id, supplier);
    }

    private static async Task<decimal> GetStock(HttpClient client, long branchId, long productId)
    {
        var response = await client.GetAsync($"/api/v1/branches/{branchId}/inventory/stock-on-hand");
        var stock = await response.Content.ReadFromJsonAsync<List<StockOnHandDto>>();
        return stock!.FirstOrDefault(s => s.ProductId == productId)?.QuantityOnHand ?? 0;
    }

    [Fact]
    public async Task StockTransfer_SendThenReceive_MovesStockBetweenBranches()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.cs");
        var (productId, _) = await ArrangeStockedProductAtColomboAsync(admin, 30);

        var colomboBefore = await GetStock(admin, ColomboBranchId, productId);
        var kandyBefore = await GetStock(admin, KandyBranchId, productId);

        var createResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/inventory/transfers", new CreateStockTransferRequest
        {
            ToBranchId = KandyBranchId,
            Lines = new List<CreateStockTransferLineRequest> { new() { ProductId = productId, Quantity = 5 } },
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var transfer = await createResponse.Content.ReadFromJsonAsync<StockTransferDto>();

        // Requested: no stock movement yet.
        (await GetStock(admin, ColomboBranchId, productId)).Should().Be(colomboBefore);

        var sendResponse = await admin.PostAsync($"/api/v1/branches/{ColomboBranchId}/inventory/transfers/{transfer!.Id}/send", null);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (colomboBefore - await GetStock(admin, ColomboBranchId, productId)).Should().Be(5);

        var receiveResponse = await admin.PostAsync($"/api/v1/branches/{KandyBranchId}/inventory/transfers/{transfer.Id}/receive", null);
        receiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetStock(admin, KandyBranchId, productId) - kandyBefore).Should().Be(5);
    }

    [Fact]
    public async Task StockCount_Discrepancy_PostsCorrectingLedgerEntry()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.cs");
        var (productId, _) = await ArrangeStockedProductAtColomboAsync(admin, 20);
        var systemQuantity = await GetStock(admin, ColomboBranchId, productId);

        var createResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/inventory/counts", new CreateStockCountRequest
        {
            ProductIds = new List<long> { productId },
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var count = await createResponse.Content.ReadFromJsonAsync<StockCountDto>();

        var countedQuantity = systemQuantity - 3;
        var submitResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/inventory/counts/{count!.Id}/lines",
            new List<SubmitStockCountLineRequest> { new() { ProductId = productId, CountedQuantity = countedQuantity } });
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var completeResponse = await admin.PostAsync($"/api/v1/branches/{ColomboBranchId}/inventory/counts/{count.Id}/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetStock(admin, ColomboBranchId, productId)).Should().Be(countedQuantity);
    }

    [Fact]
    public async Task StockCount_CompleteWithoutCountingEveryLine_Returns409()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.cs");
        var (productId, _) = await ArrangeStockedProductAtColomboAsync(admin, 15);

        var createResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/inventory/counts", new CreateStockCountRequest
        {
            ProductIds = new List<long> { productId },
        });
        var count = await createResponse.Content.ReadFromJsonAsync<StockCountDto>();

        var completeResponse = await admin.PostAsync($"/api/v1/branches/{ColomboBranchId}/inventory/counts/{count!.Id}/complete", null);

        completeResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PurchaseInvoice_PartialThenFullPayment_TransitionsStatusCorrectly()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.cs");
        var (_, supplier) = await ArrangeStockedProductAtColomboAsync(admin, 1);

        var invoiceResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/purchase-invoices", new CreatePurchaseInvoiceRequest
        {
            SupplierId = supplier.Id,
            SupplierInvoiceNumber = $"TEST-{Guid.NewGuid():N}",
            InvoiceDate = DateTime.UtcNow,
            SubTotal = 1000,
            TaxTotal = 180,
        });
        invoiceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<PurchaseInvoiceDto>();
        invoice!.GrandTotal.Should().Be(1180);
        invoice.Status.Should().Be("Unpaid");

        var partialPayment = await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/purchase-invoices/{invoice.Id}/payments",
            new RecordSupplierPaymentRequest { Amount = 500, Method = PaymentMethod.Cash });
        partialPayment.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterPartial = await partialPayment.Content.ReadFromJsonAsync<PurchaseInvoiceDto>();
        afterPartial!.Status.Should().Be("PartiallyPaid");

        var overpayResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/purchase-invoices/{invoice.Id}/payments",
            new RecordSupplierPaymentRequest { Amount = 9999, Method = PaymentMethod.Cash });
        overpayResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var finalPayment = await admin.PostAsJsonAsync($"/api/v1/branches/{ColomboBranchId}/purchase-invoices/{invoice.Id}/payments",
            new RecordSupplierPaymentRequest { Amount = 680, Method = PaymentMethod.BankTransfer, ReferenceNo = "TEST-REF" });
        finalPayment.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterFinal = await finalPayment.Content.ReadFromJsonAsync<PurchaseInvoiceDto>();
        afterFinal!.Status.Should().Be("Paid");
        afterFinal.AmountPaid.Should().Be(1180);
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
    }
}
