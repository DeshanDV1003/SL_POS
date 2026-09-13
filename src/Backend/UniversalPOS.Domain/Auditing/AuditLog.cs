namespace UniversalPOS.Domain.Auditing;

/// <summary>Immutable, append-only. Never updated or deleted after insert.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public long? TerminalId { get; set; }
    public long? UserId { get; set; }

    /// <summary>e.g. "Sale.Void", "Product.PriceChanged", "User.LoggedIn".</summary>
    public string ActionCode { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;

    /// <summary>JSON snapshot diff. The one legitimate JSON use in this schema — a diff record, not queryable business data.</summary>
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? IpAddress { get; set; }
}
