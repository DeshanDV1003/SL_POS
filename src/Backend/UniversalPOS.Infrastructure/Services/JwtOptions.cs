namespace UniversalPOS.Infrastructure.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    /// <summary>Base64-encoded signing key, at least 256 bits. Never committed to source control in a real deployment.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
