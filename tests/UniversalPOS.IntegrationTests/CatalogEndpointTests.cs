using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Catalog.Dtos;
using UniversalPOS.Application.Identity.Dtos;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>Exercises Phase 4 master-data endpoints against the real database — no mocks.</summary>
[Collection("Integration")]
public class CatalogEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;

    public CatalogEndpointTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task GetProducts_ReturnsSeededSupermarketProducts()
    {
        var client = await CreateAuthenticatedClientAsync("admin.lfm");

        var response = await client.GetAsync("/api/v1/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductSummaryDto>>();
        products.Should().Contain(p => p.Sku == "LFM-GR-001");
    }

    [Fact]
    public async Task FindByBarcode_WithSeededBarcode_ReturnsProduct()
    {
        var client = await CreateAuthenticatedClientAsync("admin.lfm");

        var response = await client.GetAsync("/api/v1/products/by-barcode/4791234500019");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductSummaryDto>();
        product!.Sku.Should().Be("LFM-GR-001");
    }

    [Fact]
    public async Task FindByBarcode_WithUnknownBarcode_Returns404()
    {
        var client = await CreateAuthenticatedClientAsync("admin.lfm");

        var response = await client.GetAsync("/api/v1/products/by-barcode/0000000000000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_Returns409()
    {
        var client = await CreateAuthenticatedClientAsync("admin.lfm");
        var units = await (await client.GetAsync("/api/v1/units")).Content.ReadFromJsonAsync<List<UnitDto>>();
        var unitId = units!.First().Id;

        var request = new CreateProductRequest
        {
            Sku = "LFM-GR-001", // already seeded
            Name = "Duplicate SKU test",
            UnitId = unitId,
            CostPrice = 1,
            SellingPrice = 2,
        };

        var response = await client.PostAsJsonAsync("/api/v1/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateProduct_WithMinSellingPriceAboveSellingPrice_Returns400()
    {
        var client = await CreateAuthenticatedClientAsync("admin.lfm");
        var units = await (await client.GetAsync("/api/v1/units")).Content.ReadFromJsonAsync<List<UnitDto>>();
        var unitId = units!.First().Id;

        var request = new CreateProductRequest
        {
            Sku = "LFM-TEST-INVALID",
            Name = "Invalid pricing",
            UnitId = unitId,
            CostPrice = 1,
            SellingPrice = 10,
            MinSellingPrice = 20,
        };

        var response = await client.PostAsJsonAsync("/api/v1/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProduct_WithoutProductManagePermission_Returns403()
    {
        // cashier.lfm is deliberately avoided here: AuthEndpointTests' lockout test locks that
        // account, and tests now share one database via the "Integration" collection.
        var client = await CreateAuthenticatedClientAsync("cashier.cs"); // Cashier role has no product.manage

        var response = await client.PostAsJsonAsync("/api/v1/products", new CreateProductRequest
        {
            Sku = "SHOULD-NOT-BE-CREATED",
            Name = "Blocked",
            UnitId = 1,
            CostPrice = 1,
            SellingPrice = 2,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAndGetCustomer_RoundTrips()
    {
        var client = await CreateAuthenticatedClientAsync("admin.lfm");

        var createResponse = await client.PostAsJsonAsync("/api/v1/customers", new Application.Crm.Dtos.CreateCustomerRequest
        {
            Name = "Kamal Perera",
            Phone = "0771234567",
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await client.GetAsync("/api/v1/customers");
        var customers = await listResponse.Content.ReadFromJsonAsync<List<Application.Crm.Dtos.CustomerDto>>();
        customers.Should().Contain(c => c.Name == "Kamal Perera");
    }
}
