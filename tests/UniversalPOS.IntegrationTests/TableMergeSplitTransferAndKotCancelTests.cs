using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Restaurant.Dtos;
using UniversalPOS.Domain.Restaurant;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the Phase 7 gap-fill features against the real database: table
/// transfer, merging two orders, splitting an order across tables, standalone
/// takeaway/delivery orders, and KOT/BOT cancellation with a real audit trail.
/// </summary>
[Collection("Integration")]
public class TableMergeSplitTransferAndKotCancelTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 1; // Ceylon Spice - Colombo 07

    public TableMergeSplitTransferAndKotCancelTests(CustomWebApplicationFactory factory)
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

    private async Task<(long OrderId, long TableId)> OpenAnAvailableTableAsync(HttpClient waiter)
    {
        var floors = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var table = floors!.SelectMany(f => f.Tables).First(t => t.Status == "Available");

        var openResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tables/{table.Id}/open", new OpenTableRequest { OrderType = OrderType.DineIn });
        var order = await openResponse.Content.ReadFromJsonAsync<OrderDto>();
        return (order!.Id, table.Id);
    }

    [Fact]
    public async Task TransferTable_MovesOrderAndFreesOldTable()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var (orderId, oldTableId) = await OpenAnAvailableTableAsync(waiter);

        var floorsBefore = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var newTable = floorsBefore!.SelectMany(f => f.Tables).First(t => t.Status == "Available");

        var response = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/transfer-table", new TransferTableRequest { NewTableId = newTable.Id });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var floorsAfter = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var tables = floorsAfter!.SelectMany(f => f.Tables).ToList();
        tables.First(t => t.Id == oldTableId).Status.Should().Be("Available");
        tables.First(t => t.Id == newTable.Id).Status.Should().Be("Occupied");
        tables.First(t => t.Id == newTable.Id).OpenOrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task MergeOrders_CombinesLinesAndFreesSourceTable()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First();

        var (order1, _) = await OpenAnAvailableTableAsync(waiter);
        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{order1}/lines", new List<AddOrderLineRequest> { new() { ProductId = product.Id, Quantity = 1 } });

        var (order2, sourceTableId) = await OpenAnAvailableTableAsync(waiter);
        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{order2}/lines", new List<AddOrderLineRequest> { new() { ProductId = product.Id, Quantity = 2 } });

        var response = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{order2}/merge", new MergeOrdersRequest { TargetOrderId = order1 });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var merged = await response.Content.ReadFromJsonAsync<OrderDto>();
        merged!.Lines.Should().HaveCount(2);

        var floorsAfter = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        floorsAfter!.SelectMany(f => f.Tables).First(t => t.Id == sourceTableId).Status.Should().Be("Available");

        var sourceOrder = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/orders/{order2}")).Content.ReadFromJsonAsync<OrderDto>();
        sourceOrder!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task SplitOrder_MovesSelectedLinesToNewTable()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var productA = products![0];
        var productB = products[1];

        var (orderId, _) = await OpenAnAvailableTableAsync(waiter);
        var addResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/lines", new List<AddOrderLineRequest>
        {
            new() { ProductId = productA.Id, Quantity = 1 },
            new() { ProductId = productB.Id, Quantity = 1 },
        });
        var orderWithLines = await addResponse.Content.ReadFromJsonAsync<OrderDto>();
        var lineToSplit = orderWithLines!.Lines.First(l => l.ProductId == productB.Id);

        var floors = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var newTable = floors!.SelectMany(f => f.Tables).First(t => t.Status == "Available");

        var splitResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/split", new SplitOrderRequest
        {
            NewTableId = newTable.Id,
            OrderLineIds = new List<long> { lineToSplit.Id },
        });
        splitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newOrder = await splitResponse.Content.ReadFromJsonAsync<OrderDto>();
        newOrder!.Lines.Should().ContainSingle(l => l.ProductId == productB.Id);

        var originalAfter = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/orders/{orderId}")).Content.ReadFromJsonAsync<OrderDto>();
        originalAfter!.Lines.Should().ContainSingle(l => l.ProductId == productA.Id);
    }

    [Fact]
    public async Task CreateStandaloneOrder_Delivery_RequiresAddress()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");

        var missingAddress = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/standalone", new CreateStandaloneOrderRequest
        {
            OrderType = OrderType.Delivery,
        });
        missingAddress.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var withAddress = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/standalone", new CreateStandaloneOrderRequest
        {
            OrderType = OrderType.Delivery,
            ContactPhone = "0770000000",
            DeliveryAddress = "1 Test Road",
            DeliveryFee = 200,
        });
        withAddress.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await withAddress.Content.ReadFromJsonAsync<OrderDto>();
        order!.TableId.Should().BeNull();
        order.DeliveryFee.Should().Be(200);
    }

    [Fact]
    public async Task CancelTicket_ByUserWithoutPermission_Returns403()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var kitchenProduct = products!.First(p => p.Sku == "CS-SE-001");

        var (orderId, _) = await OpenAnAvailableTableAsync(waiter);
        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/lines", new List<AddOrderLineRequest> { new() { ProductId = kitchenProduct.Id, Quantity = 1 } });
        var tickets = await (await waiter.PostAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/send-to-kitchen", null)).Content.ReadFromJsonAsync<List<PreparationTicketDto>>();

        var response = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tickets/{tickets![0].Id}/cancel", new CancelTicketRequest { Reason = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CancelTicket_ByAuthorizedUser_CancelsTicketAndOrderLine()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var manager = await CreateAuthenticatedClientAsync("manager.cs");
        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var kitchenProduct = products!.First(p => p.Sku == "CS-RC-001");

        var (orderId, _) = await OpenAnAvailableTableAsync(waiter);
        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/lines", new List<AddOrderLineRequest> { new() { ProductId = kitchenProduct.Id, Quantity = 1 } });
        var tickets = await (await waiter.PostAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/send-to-kitchen", null)).Content.ReadFromJsonAsync<List<PreparationTicketDto>>();

        var response = await manager.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tickets/{tickets![0].Id}/cancel", new CancelTicketRequest { Reason = "Kitchen out of stock" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await response.Content.ReadFromJsonAsync<PreparationTicketDto>();
        cancelled!.Status.Should().Be("Cancelled");

        var order = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/orders/{orderId}")).Content.ReadFromJsonAsync<OrderDto>();
        order!.Lines.First(l => l.ProductId == kitchenProduct.Id).KotStatus.Should().Be("Cancelled");

        var secondCancel = await manager.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tickets/{tickets[0].Id}/cancel", new CancelTicketRequest { Reason = "again" });
        secondCancel.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
    }
}
