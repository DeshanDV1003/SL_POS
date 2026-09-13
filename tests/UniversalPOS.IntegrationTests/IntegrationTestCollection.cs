using Xunit;

namespace UniversalPOS.IntegrationTests;

/// <summary>
/// All integration test classes share one CustomWebApplicationFactory (and therefore
/// one physical LocalDB test database) instead of each spinning up its own. Without
/// this, xUnit's default parallel test-class execution runs multiple factories'
/// startup (migrate + seed) concurrently against the same database, racing each other
/// and corrupting the seed data — a real bug this exact setup surfaced once a second
/// test class was added.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
}
