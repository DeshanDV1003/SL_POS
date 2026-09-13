using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UniversalPOS.Application.Identity.Dtos;
using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Exercises the real API host, real SQL Server (LocalDB) database, real migrations,
/// and the real seed data — no mocks. Verifies the login/authorization contract that
/// the rest of the platform depends on.
/// </summary>
[Collection("Integration")]
public class AuthEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidSeededCredentials_ReturnsTokensAndPermissions()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = "admin.cs",
            Password = "Passw0rd!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResult>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.User.Permissions.Should().Contain("company.manage");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithoutLeakingDetail()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = "admin.cs",
            Password = "definitely-wrong",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Exception");
        body.Should().NotContain("System.");
    }

    [Fact]
    public async Task Login_WithUnknownUsername_Returns401IdenticalToWrongPassword()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = "no-such-user",
            Password = "whatever",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksAccount()
    {
        var client = _factory.CreateClient();
        const string username = "qa.lockouttest"; // dedicated seed user reserved for this test only

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Username = username, Password = "wrong" });
        }

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Username = username, Password = "Passw0rd!" });

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/companies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTokenLackingPermission_Returns403()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Username = "manager.lfm", Password = "Passw0rd!" });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResult>();

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        var response = await client.GetAsync("/api/v1/companies"); // requires company.manage, which Manager does not hold

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithSufficientPermission_ReturnsData()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Username = "admin.lfm", Password = "Passw0rd!" });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResult>();

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        var response = await client.GetAsync("/api/v1/companies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var companies = await response.Content.ReadFromJsonAsync<List<Application.Organization.Dtos.CompanyDto>>();
        companies.Should().NotBeNullOrEmpty();
    }
}
