using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// Runs the real API host against a dedicated LocalDB database (not mocked, not
/// SQLite-in-memory) so integration tests exercise the actual SQL Server provider,
/// real migrations, and the real seed data path.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=UniversalPosTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
            });
        });
    }
}
