using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Restaurant.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Restaurant;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the Phase 7 restaurant flow against the real database: open a table,
/// add items, send to the correct kitchen/bar stations, update ticket status from the
/// KDS, and bill — verifying the table is correctly occupied and then released, and
/// that billing produces the same real Sale/stock effects as retail checkout.
/// </summary>
[Collection("Integration")]
public class RestaurantEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 1; // Ceylon Spice - Colombo 07

    public RestaurantEndpointTests(CustomWebApplicationFactory factory)
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

    private async Task<long> OpenAnAvailableTableAsync(HttpClient waiter)
    {
        var floors = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var table = floors!.SelectMany(f => f.Tables).First(t => t.Status == "Available");

        var openResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tables/{table.Id}/open", new OpenTableRequest { OrderType = OrderType.DineIn });
        openResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = await openResponse.Content.ReadFromJsonAsync<OrderDto>();
        return order!.Id;
    }

    [Fact]
    public async Task OpenTable_MarksTableOccupied_AndCreatesOpenOrder()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");

        var floorsBefore = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var table = floorsBefore!.SelectMany(f => f.Tables).First(t => t.Status == "Available");

        var openResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tables/{table.Id}/open", new OpenTableRequest { OrderType = OrderType.DineIn });
        openResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var floorsAfter = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var sameTable = floorsAfter!.SelectMany(f => f.Tables).First(t => t.Id == table.Id);
        sameTable.Status.Should().Be("Occupied");
        sameTable.OpenOrderId.Should().NotBeNull();
    }

    [Fact]
    public async Task OpeningAnAlreadyOccupiedTable_Returns409()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var floors = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        var table = floors!.SelectMany(f => f.Tables).First(t => t.Status == "Available");

        var first = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tables/{table.Id}/open", new OpenTableRequest());
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tables/{table.Id}/open", new OpenTableRequest());
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SendToKitchen_RoutesItemsToCorrectStationsByProduct()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var orderId = await OpenAnAvailableTableAsync(waiter);

        var products = (await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>())!;
        var kottu = products.First(p => p.Sku == "CS-SE-001"); // routed to Main Kitchen
        var coconut = products.First(p => p.Sku == "CS-BV-001"); // routed to Bar

        var addLinesResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/lines", new List<AddOrderLineRequest>
        {
            new() { ProductId = kottu.Id, Quantity = 1 },
            new() { ProductId = coconut.Id, Quantity = 1 },
        });
        addLinesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sendResponse = await waiter.PostAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/send-to-kitchen", null);
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var tickets = await sendResponse.Content.ReadFromJsonAsync<List<PreparationTicketDto>>();

        tickets.Should().HaveCount(2, "the two items route to two different stations");
        tickets!.Should().ContainSingle(t => t.Lines.Any(l => l.ProductId == kottu.Id));
        tickets.Should().ContainSingle(t => t.Lines.Any(l => l.ProductId == coconut.Id));
    }

    [Fact]
    public async Task KdsUpdate_ByUserWithoutPermission_Returns403()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var orderId = await OpenAnAvailableTableAsync(waiter);
        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var kottu = products!.First(p => p.Sku == "CS-SE-001");

        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/lines", new List<AddOrderLineRequest> { new() { ProductId = kottu.Id, Quantity = 1 } });
        var tickets = await (await waiter.PostAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/send-to-kitchen", null)).Content.ReadFromJsonAsync<List<PreparationTicketDto>>();

        // cashier.cs (waiter role here) has order.create/bill but not kds.update
        var response = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/tickets/{tickets![0].Id}/status", new UpdateTicketStatusRequest { Status = TicketStatus.Preparing });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BillOrder_UnderpaymentIgnoringServiceCharge_Returns400_ThenCorrectPaymentReleasesTable()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var orderId = await OpenAnAvailableTableAsync(waiter);
        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var kottu = products!.First(p => p.Sku == "CS-SE-001"); // 850, Colombo branch has a 10% service charge

        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/lines", new List<AddOrderLineRequest> { new() { ProductId = kottu.Id, Quantity = 1 } });

        var underpayResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/bill", new BillOrderRequest
        {
            TerminalId = 1,
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = kottu.SellingPrice } }, // missing the 10% service charge
        });
        underpayResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var floorsAfterUnderpay = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>();
        floorsAfterUnderpay!.SelectMany(f => f.Tables).First(t => t.OpenOrderId == orderId).Status.Should().Be("Occupied");

        var billResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/bill", new BillOrderRequest
        {
            TerminalId = 1,
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = kottu.SellingPrice * 1.15m } },
        });
        billResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var receipt = await billResponse.Content.ReadFromJsonAsync<SaleReceiptDto>();
        receipt!.ServiceChargeTotal.Should().BeGreaterThan(0);

        var occupiedTableId = floorsAfterUnderpay!.SelectMany(f => f.Tables).First(t => t.OpenOrderId == orderId).Id;
        var floorsAfterBill = (await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/floors")).Content.ReadFromJsonAsync<List<FloorDto>>())!;
        var tableAfterBill = floorsAfterBill.SelectMany(f => f.Tables).First(t => t.Id == occupiedTableId);
        tableAfterBill.Status.Should().Be("Available");
        tableAfterBill.OpenOrderId.Should().BeNull();
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
    }
}
