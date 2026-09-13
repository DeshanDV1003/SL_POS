using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Cash.Dtos;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Cash;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the Phase 8 cash management flow against the real database: opening a
/// shift, taking a cash sale against it, recording a cash movement, and closing it
/// with a correctly derived expected-cash/variance figure — then the day-end report's
/// generate-then-lock behavior.
/// </summary>
[Collection("Integration")]
public class CashEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 3; // Lanka Fresh Mart - Nugegoda

    public CashEndpointTests(CustomWebApplicationFactory factory)
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

    /// <summary>
    /// A stocked product plus one of Lanka Fresh Mart's 3 seeded terminals, picked by
    /// index — tests that open a shift must each use a distinct terminal (there's no
    /// create-terminal endpoint), otherwise a shift left open by one test collides
    /// with another test's OpenShift call on the same terminal.
    /// </summary>
    private async Task<(long ProductId, decimal Price, long TerminalId)> ArrangeStockedProductAndTerminalAsync(HttpClient admin, int terminalIndex = 0)
    {
        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First(p => p.Sku == "LFM-BV-001");

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Cash Test Supplier {Guid.NewGuid():N}" });
        var supplier = await supplierResponse.Content.ReadFromJsonAsync<SupplierDto>();
        await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/goods-received-notes", new ReceiveGoodsRequest
        {
            SupplierId = supplier!.Id,
            Lines = new List<ReceiveGoodsLineRequest> { new() { ProductId = product.Id, QuantityReceived = 50, UnitCost = 100 } },
        });

        var terminals = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/terminals")).Content.ReadFromJsonAsync<List<TerminalInfo>>();
        return (product.Id, product.SellingPrice, terminals![terminalIndex % terminals.Count].Id);
    }

    [Fact]
    public async Task OpenShift_Twice_OnSameTerminal_Returns409()
    {
        var cashier = await CreateAuthenticatedClientAsync("manager.lfm"); // manager also holds cash.shift.open
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (_, _, terminalId) = await ArrangeStockedProductAndTerminalAsync(admin, terminalIndex: 0);

        var first = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts", new OpenShiftRequest { TerminalId = terminalId, OpeningFloat = 1000 });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts", new OpenShiftRequest { TerminalId = terminalId, OpeningFloat = 500 });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CloseShift_ComputesExpectedCashFromOpeningFloatSalesAndMovements()
    {
        // admin.lfm both arranges fixtures and opens/closes the shift here: with manager.lfm
        // already holding an open shift from OpenShift_Twice_OnSameTerminal_Returns409 (a
        // user may only hold one open shift at a time), a distinct user is needed anyway.
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, terminalId) = await ArrangeStockedProductAndTerminalAsync(admin, terminalIndex: 1);
        var cashier = admin;

        var openResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts", new OpenShiftRequest { TerminalId = terminalId, OpeningFloat = 2000 });
        var shift = await openResponse.Content.ReadFromJsonAsync<ShiftDto>();

        var saleResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });
        saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var movementResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts/{shift!.Id}/movements", new RecordCashMovementRequest
        {
            MovementType = CashMovementType.Petty,
            Amount = 50,
            Reason = "Test petty cash",
        });
        movementResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var expectedCash = 2000 + price - 50;
        var closeResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts/{shift.Id}/close", new CloseShiftRequest { ClosingFloatCounted = expectedCash - 10 });
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var closed = await closeResponse.Content.ReadFromJsonAsync<ShiftDto>();

        closed!.ExpectedCash.Should().Be(expectedCash);
        closed.VarianceAmount.Should().Be(-10);
        closed.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task CloseShift_ByUserWithoutPermission_Returns403()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (_, _, terminalId) = await ArrangeStockedProductAndTerminalAsync(admin, terminalIndex: 2);
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm"); // no cash.shift.close

        var openResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts", new OpenShiftRequest { TerminalId = terminalId, OpeningFloat = 1000 });
        var shift = await openResponse.Content.ReadFromJsonAsync<ShiftDto>();

        var closeResponse = await cashier.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts/{shift!.Id}/close", new CloseShiftRequest { ClosingFloatCounted = 1000 });

        closeResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DayEndReport_OnceFinalized_IsLockedAgainstNewSalesThatSameDay()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        var (productId, price, terminalId) = await ArrangeStockedProductAndTerminalAsync(admin);
        var manager = await CreateAuthenticatedClientAsync("manager.lfm");

        await manager.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reportResponse = await manager.GetAsync($"/api/v1/branches/{BranchId}/day-end-report?businessDate={today:yyyy-MM-dd}");
        reportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await reportResponse.Content.ReadFromJsonAsync<DayEndReportDto>();

        var finalizeResponse = await manager.PostAsync($"/api/v1/branches/{BranchId}/day-end-report/{report!.Id}/finalize", null);
        finalizeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondFinalize = await manager.PostAsync($"/api/v1/branches/{BranchId}/day-end-report/{report.Id}/finalize", null);
        secondFinalize.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A sale made after finalization must not change the locked report's totals.
        var transactionCountAtFinalize = report.TransactionCount;
        await manager.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
        {
            TerminalId = terminalId,
            Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
            Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
        });

        var afterResponse = await manager.GetAsync($"/api/v1/branches/{BranchId}/day-end-report?businessDate={today:yyyy-MM-dd}");
        var afterReport = await afterResponse.Content.ReadFromJsonAsync<DayEndReportDto>();
        afterReport!.TransactionCount.Should().Be(transactionCountAtFinalize, "a finalized Z-report must not be recomputed");
        afterReport.Status.Should().Be("Finalized");
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
