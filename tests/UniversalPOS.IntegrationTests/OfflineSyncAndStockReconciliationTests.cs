using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Inventory.Dtos;
using UniversalPOS.Application.Organization.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Phase 11: an offline-synced sale (IsOfflineSync=true) is never rejected for
/// insufficient stock — negative stock has never been blocked anywhere in this
/// codebase (see StockService.PostMovementAsync) — but only the offline-sync path
/// raises a real StockReconciliationFlag for manager review; a normal online sale
/// that happens to oversell does not. Also covers the ClientIdempotencyKey
/// requirement for offline syncs and the terminal heartbeat endpoint.
/// </summary>
[Collection("Integration")]
public class OfflineSyncAndStockReconciliationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 3; // Lanka Fresh Mart - Nugegoda

    public OfflineSyncAndStockReconciliationTests(CustomWebApplicationFactory factory)
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

    /// <summary>A freshly created, uniquely stocked product so this test's oversell doesn't collide with other tests' stock on the shared seeded SKUs.</summary>
    private async Task<(long ProductId, decimal Price, long TerminalId, decimal StockedQuantity)> ArrangeLightlyStockedProductAsync(HttpClient admin, decimal quantityReceived)
    {
        var units = await (await admin.GetAsync("/api/v1/units")).Content.ReadFromJsonAsync<List<UnitInfo>>();
        var productResponse = await admin.PostAsJsonAsync("/api/v1/products", new CreateProductInfo
        {
            Sku = $"LFM-OFFLINE-{Guid.NewGuid():N}"[..24],
            Name = "Offline Sync Test Product",
            UnitId = units!.First().Id,
            CostPrice = 50,
            SellingPrice = 100,
        });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductInfo>();

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Offline Sync Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();
        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product!.Id, QuantityReceived = quantityReceived, UnitCost = 50 } },
        });

        var terminals = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/terminals")).Content.ReadFromJsonAsync<List<TerminalInfo>>();
        return (product.Id, product.SellingPrice, terminals!.First().Id, quantityReceived);
    }

    [Fact]
    public async Task OfflineSync_WithoutClientIdempotencyKey_Returns400()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, terminalId, _) = await ArrangeLightlyStockedProductAsync(admin, 10);

        var response = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            IsOfflineSync = true,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OfflineSync_Oversell_PostsTheSaleAnywayAndRaisesAReconciliationFlag()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, terminalId, stocked) = await ArrangeLightlyStockedProductAsync(admin, 5);

        var oversellQuantity = stocked + 3; // 8 units against 5 in stock -> -3 on hand
        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            IsOfflineSync = true,
            ClientIdempotencyKey = $"offline-{Guid.NewGuid():N}",
            ClientCreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = oversellQuantity } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * oversellQuantity } },
        });

        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created, "an offline-synced sale is never rejected for insufficient stock");
        var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();
        receipt!.IsOfflineSync.Should().BeTrue();

        var stockResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/inventory/stock-on-hand");
        var stock = await stockResponse.Content.ReadFromJsonAsync<List<StockOnHandInfo>>();
        stock!.First(s => s.ProductId == productId).QuantityOnHand.Should().Be(-3);

        var flagsResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/inventory/reconciliation-flags?openOnly=true");
        flagsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var flags = await flagsResponse.Content.ReadFromJsonAsync<List<StockReconciliationFlagDto>>();
        flags!.Should().ContainSingle(f => f.ProductId == productId && f.SaleHeaderId == receipt.Id && f.ShortfallQuantity == 3 && f.Status == "Open");
    }

    [Fact]
    public async Task OnlineSale_Oversell_DoesNotRaiseAReconciliationFlag()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, terminalId, stocked) = await ArrangeLightlyStockedProductAsync(admin, 2);

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = stocked + 5 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * (stocked + 5) } },
        });
        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created, "negative stock is never blocked, online or offline");
        var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();
        receipt!.IsOfflineSync.Should().BeFalse();

        var flagsResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/inventory/reconciliation-flags?openOnly=false");
        var flags = await flagsResponse.Content.ReadFromJsonAsync<List<StockReconciliationFlagDto>>();
        flags!.Should().NotContain(f => f.SaleHeaderId == receipt.Id, "only an offline-synced oversell raises a reconciliation flag");
    }

    [Fact]
    public async Task OfflineSync_SameIdempotencyKeyRetried_ReturnsTheSameSaleAndDoesNotDoubleDeductStock()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, terminalId, _) = await ArrangeLightlyStockedProductAsync(admin, 20);
        var key = $"offline-retry-{Guid.NewGuid():N}";

        var request = new CreateSaleRequest
        {
            TerminalId = terminalId,
            IsOfflineSync = true,
            ClientIdempotencyKey = key,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 2 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * 2 } },
        };

        var first = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", request);
        var second = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", request);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstReceipt = await first.Content.ReadFromJsonAsync<SaleReceiptDto>();
        var secondReceipt = await second.Content.ReadFromJsonAsync<SaleReceiptDto>();
        secondReceipt!.Id.Should().Be(firstReceipt!.Id, "a retried sync of the same offline sale must not create a second sale");

        var stock = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/inventory/stock-on-hand")).Content.ReadFromJsonAsync<List<StockOnHandInfo>>();
        stock!.First(s => s.ProductId == productId).QuantityOnHand.Should().Be(18, "the retried sync must not deduct stock twice");
    }

    [Fact]
    public async Task ResolveReconciliationFlag_TwiceIsRejected()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, terminalId, stocked) = await ArrangeLightlyStockedProductAsync(admin, 1);

        var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            IsOfflineSync = true,
            ClientIdempotencyKey = $"offline-{Guid.NewGuid():N}",
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = stocked + 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price * (stocked + 1) } },
        });
        var receipt = await saleResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();

        var flags = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/inventory/reconciliation-flags?openOnly=true"))
            .Content.ReadFromJsonAsync<List<StockReconciliationFlagDto>>();
        var flag = flags!.Single(f => f.SaleHeaderId == receipt!.Id);

        var firstResolve = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/inventory/reconciliation-flags/{flag.Id}/resolve",
            new ResolveStockReconciliationFlagRequest { ResolutionNotes = "Physical count adjusted" });
        firstResolve.StatusCode.Should().Be(HttpStatusCode.OK);
        (await firstResolve.Content.ReadFromJsonAsync<StockReconciliationFlagDto>())!.Status.Should().Be("Resolved");

        var secondResolve = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/inventory/reconciliation-flags/{flag.Id}/resolve",
            new ResolveStockReconciliationFlagRequest());
        secondResolve.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReconciliationFlags_ByUserWithoutPermission_Returns403()
    {
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm");

        var response = await cashier.GetAsync($"/api/v1/branches/{BranchId}/inventory/reconciliation-flags?openOnly=true");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TerminalHeartbeat_UpdatesLastSeenAtUtc()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var terminals = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/terminals")).Content.ReadFromJsonAsync<List<TerminalInfo>>();
        var terminal = terminals!.First();

        var before = DateTime.UtcNow;
        var response = await admin.PostAsync($"/api/v1/branches/{BranchId}/terminals/{terminal.Id}/heartbeat", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/terminals")).Content.ReadFromJsonAsync<List<TerminalInfo>>();
        var updated = after!.First(t => t.Id == terminal.Id);
        updated.LastSeenAtUtc.Should().NotBeNull();
        updated.LastSeenAtUtc!.Value.Should().BeOnOrAfter(before.AddSeconds(-2));
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
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

    private class TerminalInfo
    {
        public long Id { get; set; }
        public DateTime? LastSeenAtUtc { get; set; }
    }

    private class StockOnHandInfo
    {
        public long ProductId { get; set; }
        public decimal QuantityOnHand { get; set; }
    }
}
