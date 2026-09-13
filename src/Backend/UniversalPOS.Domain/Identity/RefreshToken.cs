namespace UniversalPOS.Domain.Identity;

public class RefreshToken
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public long? TerminalId { get; set; }

    /// <summary>SHA-256 hash of the opaque token value. The raw value is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public bool IsRevoked { get; set; }
    public long? ReplacedByTokenId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedByIp { get; set; }
}
