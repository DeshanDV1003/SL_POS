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
/// Phase 12 (software-only hardware-abstraction pieces): the KOT/BOT ticket text
/// renderer fills the gap left open at the end of Phase 7's gap-fill ("printed
/// KOT/BOT tickets... hasn't yet"). This is the real payload a kitchen/bar print
/// adapter would send verbatim per docs/architecture.md §11 — the backend never
/// talks to hardware, it only produces the text.
/// </summary>
[Collection("Integration")]
public class KotTicketPrintTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 1; // Ceylon Spice - Colombo 07

    public KotTicketPrintTests(CustomWebApplicationFactory factory)
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
        var order = await openResponse.Content.ReadFromJsonAsync<OrderDto>();
        return order!.Id;
    }

    [Fact]
    public async Task GetTicketPrintPayload_ForDineInOrder_IncludesTableAndKitchenStation()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var orderId = await OpenAnAvailableTableAsync(waiter);

        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var kottu = products!.First(p => p.Sku == "CS-SE-001"); // Main Kitchen

        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/lines",
            new List<AddOrderLineRequest> { new() { ProductId = kottu.Id, Quantity = 2, Notes = "extra spicy" } });
        var tickets = await (await waiter.PostAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/send-to-kitchen", null))
            .Content.ReadFromJsonAsync<List<PreparationTicketDto>>();
        var ticket = tickets!.Single();

        var response = await waiter.GetAsync($"/api/v1/branches/{BranchId}/tickets/{ticket.Id}/print");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        var text = await response.Content.ReadAsStringAsync();

        text.Should().Contain("KITCHEN ORDER TICKET");
        text.Should().Contain("Main Kitchen");
        text.Should().Contain(ticket.TicketNumber);
        text.Should().Contain("2x Chicken Kottu");
        text.Should().Contain("extra spicy");
        text.Should().NotContain("Order Type:", "a dine-in ticket shows the table, not a standalone order type");
    }

    [Fact]
    public async Task GetTicketPrintPayload_ForBarStationItem_LabelsItAsABarTicket()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");
        var orderId = await OpenAnAvailableTableAsync(waiter);

        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var coconut = products!.First(p => p.Sku == "CS-BV-001"); // Bar

        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/lines",
            new List<AddOrderLineRequest> { new() { ProductId = coconut.Id, Quantity = 1 } });
        var tickets = await (await waiter.PostAsync($"/api/v1/branches/{BranchId}/orders/{orderId}/send-to-kitchen", null))
            .Content.ReadFromJsonAsync<List<PreparationTicketDto>>();
        var ticket = tickets!.Single();

        var text = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/tickets/{ticket.Id}/print")).Content.ReadAsStringAsync();

        text.Should().Contain("BAR ORDER TICKET");
        text.Should().Contain("1x King Coconut");
    }

    [Fact]
    public async Task GetTicketPrintPayload_ForStandaloneTakeawayOrder_ShowsOrderTypeAndPhoneInsteadOfTable()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");

        var standaloneResponse = await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/standalone",
            new CreateStandaloneOrderRequest { OrderType = OrderType.Takeaway, ContactPhone = "0771234567" });
        var order = await standaloneResponse.Content.ReadFromJsonAsync<OrderDto>();

        var products = await (await waiter.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var riceAndCurry = products!.First(p => p.Sku == "CS-RC-001");

        await waiter.PostAsJsonAsync($"/api/v1/branches/{BranchId}/orders/{order!.Id}/lines",
            new List<AddOrderLineRequest> { new() { ProductId = riceAndCurry.Id, Quantity = 1 } });
        var tickets = await (await waiter.PostAsync($"/api/v1/branches/{BranchId}/orders/{order.Id}/send-to-kitchen", null))
            .Content.ReadFromJsonAsync<List<PreparationTicketDto>>();
        var ticket = tickets!.Single();

        var text = await (await waiter.GetAsync($"/api/v1/branches/{BranchId}/tickets/{ticket.Id}/print")).Content.ReadAsStringAsync();

        text.Should().Contain("Order Type: Takeaway");
        text.Should().Contain("Phone: 0771234567");
        text.Should().NotContain("Table:");
    }

    [Fact]
    public async Task GetTicketPrintPayload_ForUnknownTicket_Returns404()
    {
        var waiter = await CreateAuthenticatedClientAsync("cashier.cs");

        var response = await waiter.GetAsync($"/api/v1/branches/{BranchId}/tickets/999999999/print");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private class ProductInfo
    {
        public long Id { get; set; }
        public string Sku { get; set; } = string.Empty;
    }
}
