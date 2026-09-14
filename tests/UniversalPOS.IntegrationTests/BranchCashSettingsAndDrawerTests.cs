using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Cash.Dtos;
using UniversalPOS.Application.Identity.Dtos;
using UniversalPOS.Application.Organization.Dtos;
using UniversalPOS.Application.Purchasing.Dtos;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Phase 8 gap-fill: per-branch mandatory-shift-to-sell, a configurable business-day
/// cutoff hour for Z-reports, the day-end-report history listing, and the (audit-only,
/// no physical hardware yet) cash-drawer-open action. Uses Lanka Fresh Mart's Nugegoda
/// branch (id 3) and always resets its cash settings back to the defaults afterwards so
/// other test classes sharing this database are unaffected.
/// </summary>
[Collection("Integration")]
public class BranchCashSettingsAndDrawerTests
{
    private readonly CustomWebApplicationFactory _factory;
    private const long BranchId = 3; // Lanka Fresh Mart - Nugegoda

    public BranchCashSettingsAndDrawerTests(CustomWebApplicationFactory factory)
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

    private static async Task ResetCashSettingsAsync(HttpClient admin)
    {
        await admin.PutAsJsonAsync($"/api/v1/branches/{BranchId}/cash-settings",
            new UpdateBranchCashSettingsRequest { RequireOpenShiftForSale = false, BusinessDayCutoffHour = 0 });
    }

    private async Task<(long ProductId, decimal Price, long TerminalId)> ArrangeStockedProductAndTerminalAsync(HttpClient admin, int terminalIndex)
    {
        var products = await (await admin.GetAsync("/api/v1/products")).Content.ReadFromJsonAsync<List<ProductInfo>>();
        var product = products!.First(p => p.Sku == "LFM-BV-001");

        var supplierResponse = await admin.PostAsJsonAsync("/api/v1/suppliers", new CreateSupplierRequest { Name = $"Cash Settings Test Supplier {Guid.NewGuid():N}" });
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
    public async Task UpdateCashSettings_ByManagerWithoutBranchManage_Returns403()
    {
        var manager = await CreateAuthenticatedClientAsync("manager.lfm"); // holds no branch.manage permission

        var response = await manager.PutAsJsonAsync($"/api/v1/branches/{BranchId}/cash-settings",
            new UpdateBranchCashSettingsRequest { RequireOpenShiftForSale = true, BusinessDayCutoffHour = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCashSettings_WithOutOfRangeCutoffHour_Returns400()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");

        var response = await admin.PutAsJsonAsync($"/api/v1/branches/{BranchId}/cash-settings",
            new UpdateBranchCashSettingsRequest { RequireOpenShiftForSale = false, BusinessDayCutoffHour = 24 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequireOpenShiftForSale_WhenEnabled_BlocksCheckoutUntilShiftIsOpened()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        try
        {
            var (productId, price, terminalId) = await ArrangeStockedProductAndTerminalAsync(admin, terminalIndex: 0);

            var updateResponse = await admin.PutAsJsonAsync($"/api/v1/branches/{BranchId}/cash-settings",
                new UpdateBranchCashSettingsRequest { RequireOpenShiftForSale = true, BusinessDayCutoffHour = 0 });
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var updated = await updateResponse.Content.ReadFromJsonAsync<BranchDto>();
            updated!.RequireOpenShiftForSale.Should().BeTrue();

            var blockedSale = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
            {
                TerminalId = terminalId,
                Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
                Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
            });
            blockedSale.StatusCode.Should().Be(HttpStatusCode.Conflict, "the branch requires an open shift before checkout");

            var openShiftResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts", new OpenShiftRequest { TerminalId = terminalId, OpeningFloat = 1000 });
            openShiftResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var shift = await openShiftResponse.Content.ReadFromJsonAsync<ShiftDto>();

            try
            {
                var allowedSale = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
                {
                    TerminalId = terminalId,
                    Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
                    Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
                });
                allowedSale.StatusCode.Should().Be(HttpStatusCode.Created, "an open shift on the terminal now satisfies the requirement");
            }
            finally
            {
                // A user may hold only one open shift at a time (enforced globally, not
                // per-branch) — leaving this open would break every other test class in
                // this shared collection that opens a shift with the same admin.lfm user.
                await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/shifts/{shift!.Id}/close", new CloseShiftRequest { ClosingFloatCounted = 1000 + price });
            }
        }
        finally
        {
            await ResetCashSettingsAsync(admin);
        }
    }

    [Fact]
    public async Task BusinessDayCutoffHour_ShiftsWhichBusinessDateASaleIsReportedUnder()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");
        try
        {
            var (productId, price, terminalId) = await ArrangeStockedProductAndTerminalAsync(admin, terminalIndex: 1);

            // Cutoff = the current UTC hour guarantees "now" falls inside [today@cutoff, tomorrow@cutoff),
            // so the sale must count toward today's business date and must NOT yet appear under tomorrow's.
            var cutoffHour = DateTime.UtcNow.Hour;
            var updateResponse = await admin.PutAsJsonAsync($"/api/v1/branches/{BranchId}/cash-settings",
                new UpdateBranchCashSettingsRequest { RequireOpenShiftForSale = false, BusinessDayCutoffHour = cutoffHour });
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var saleResponse = await admin.PostAsJsonAsync($"/api/v1/branches/{BranchId}/sales", new CreateSaleRequest
            {
                TerminalId = terminalId,
                Lines = new List<CreateSaleLineRequest> { new() { ProductId = productId, Quantity = 1 } },
                Payments = new List<CreateSalePaymentRequest> { new() { Method = PaymentMethod.Cash, Amount = price } },
            });
            saleResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var tomorrow = today.AddDays(1);

            var todayReport = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/day-end-report?businessDate={today:yyyy-MM-dd}"))
                .Content.ReadFromJsonAsync<DayEndReportDto>();
            todayReport!.TransactionCount.Should().BeGreaterThanOrEqualTo(1, "the sale falls within today's cutoff-adjusted window");

            var tomorrowReport = await (await admin.GetAsync($"/api/v1/branches/{BranchId}/day-end-report?businessDate={tomorrow:yyyy-MM-dd}"))
                .Content.ReadFromJsonAsync<DayEndReportDto>();
            tomorrowReport!.TransactionCount.Should().Be(0, "tomorrow's cutoff-adjusted window has not started yet");
        }
        finally
        {
            await ResetCashSettingsAsync(admin);
        }
    }

    [Fact]
    public async Task DayEndReportHistory_ListsGeneratedReportsDescendingByBusinessDate()
    {
        var admin = await CreateAuthenticatedClientAsync("admin.lfm");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var generateResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/day-end-report?businessDate={today:yyyy-MM-dd}");
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyResponse = await admin.GetAsync($"/api/v1/branches/{BranchId}/day-end-report/history");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<DayEndReportDto>>();

        history.Should().NotBeNullOrEmpty();
        history!.Should().Contain(r => r.BusinessDate == today);
        history.Should().BeInDescendingOrder(r => r.BusinessDate);
    }

    [Fact]
    public async Task DayEndReportHistory_ByUserWithoutReportsPermission_Returns403()
    {
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm"); // no reports.view.financial

        var response = await cashier.GetAsync($"/api/v1/branches/{BranchId}/day-end-report/history");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OpenCashDrawer_ByUserWithPermission_Returns204()
    {
        var manager = await CreateAuthenticatedClientAsync("manager.lfm"); // holds cash.drawer.open

        var response = await manager.PostAsync($"/api/v1/branches/{BranchId}/cash-drawer/open", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task OpenCashDrawer_ByUserWithoutPermission_Returns403()
    {
        var cashier = await CreateAuthenticatedClientAsync("cashier.lfm"); // does not hold cash.drawer.open

        var response = await cashier.PostAsync($"/api/v1/branches/{BranchId}/cash-drawer/open", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
