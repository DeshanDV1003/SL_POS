using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Crm.Dtos;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Inventory.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the Phase 5 purchasing -> inventory chain against the real database: a
/// purchase order approved and received must post real StockLedger rows and update
/// StockOnHand, and a stock adjustment must require a second, different authorized
/// user to approve before it touches stock at all.
/// </summary>
[Collection("Integration")]
public class InventoryAndPurchasingTests
{
    private readonly CustomWebApplicationFactory _factory;

    public InventoryAndPurchasingTests(CustomWebApplicationFactory factory)
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

    // Lanka Fresh Mart's seeded branch/company; admin.lfm/manager.lfm both operate here.
    private const long BranchId = 3;

    [Fact]
    public async Task PurchaseOrder_ApprovedAndReceived_PostsStockAndUpdatesOnHand()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Test Supplier {Guid.NewGuid():N}" });
        supplierResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();

        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductCheck>>();
        var productId = products!.First().Id;

        var beforeStock = await GetStockOnHand(admin, productId);

        var poResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/purchase-orders", new CreatePurchaseOrderRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<CreatePurchaseOrderLineRequest> { new() { ProductId = productId, Quantity = 50, UnitCost = 100 } },
        });
        poResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var po = await poResponse.Content.ReadFromJsonAsync<PurchaseOrderDto>();

        var approveResponse = await admin.PostAsync($"/api/v1/branches/{BranchId}/purchase-orders/{po!.Id}/approve", null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var grnResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier.Id,
            PurchaseOrderId = po.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = productId, QuantityReceived = 50, UnitCost = 100 } },
        });
        grnResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var afterStock = await GetStockOnHand(admin, productId);
        (afterStock - beforeStock).Should().Be(50);

        var ledgerResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/inventory/products/{productId}/ledger");
        var ledger = await ledgerResponse.Content.ReadFromJsonAsync<List<StockLedgerEntryDto>>();
        ledger.Should().Contain(l => l.MovementType == "Purchase" && l.QuantityChange == 50);
    }

    [Fact]
    public async Task StockAdjustment_CannotBeApprovedBySameUserWhoRequestedIt()
    {
        var manager = await CreateAuthenticatedClientAsync("manager.lfm");

        var products = await (await manager.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductCheck>>();
        var productId = products!.First().Id;

        var createResponse = await manager.PostAsJsonAsync($"/api/v1/branches/{BranchId}/inventory/adjustments", new CreateStockAdjustmentRequest
        {
            Reason = Domain.Inventory.StockAdjustmentReason.Wastage,
            Notes = "Integration test wastage",
            Lines = new List<CreateStockAdjustmentLineRequest> { new() { ProductId = productId, QuantityChange = -1 } },
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var adjustment = await createResponse.Content.ReadFromJsonAsync<StockAdjustmentDto>();

        var selfApproveResponse = await manager.PostAsync($"/api/v1/branches/{BranchId}/inventory/adjustments/{adjustment!.Id}/approve", null);

        selfApproveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StockAdjustment_ApprovedByDifferentUser_UpdatesStockAndCannotBeApprovedTwice()
    {
        var manager = await CreateAuthenticatedClientAsync("manager.lfm");
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");

        var products = await (await manager.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductCheck>>();
        var productId = products!.First().Id;

        var beforeStock = await GetStockOnHand(manager, productId);

        var createResponse = await manager.PostAsJsonAsync($"/api/v1/branches/{BranchId}/inventory/adjustments", new CreateStockAdjustmentRequest
        {
            Reason = Domain.Inventory.StockAdjustmentReason.CountDiscrepancy,
            Lines = new List<CreateStockAdjustmentLineRequest> { new() { ProductId = productId, QuantityChange = 3 } },
        });
        var adjustment = await createResponse.Content.ReadFromJsonAsync<StockAdjustmentDto>();

        var approveResponse = await admin.PostAsync($"/api/v1/branches/{BranchId}/inventory/adjustments/{adjustment!.Id}/approve", null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterStock = await GetStockOnHand(manager, productId);
        (afterStock - beforeStock).Should().Be(3);

        var secondApprove = await admin.PostAsync($"/api/v1/branches/{BranchId}/inventory/adjustments/{adjustment.Id}/approve", null);
        secondApprove.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static async Task<decimal> GetStockOnHand(HttpClient client, long productId)
    {
        var response = await client.GetAsync($"/api/v1/branches/{BranchId}/inventory/stock-on-hand");
        var stock = await response.Content.ReadFromJsonAsync<List<StockOnHandDto>>();
        return stock!.FirstOrDefault(s => s.ProductId == productId)?.QuantityOnHand ?? 0;
    }

    private class ProductCheck
    {
        public long Id { get; set; }
    }
}
