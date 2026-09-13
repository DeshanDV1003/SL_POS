namespace UniversalPOS.Domain.Auditing;

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>Backs the manager-approval workflow for sensitive actions (void, refund, price override, ...).</summary>
public class ApprovalRequest
{
    public long Id { get; set; }
    public long RequestedByUserId { get; set; }
    public long? ApprovedByUserId { get; set; }

    public string ActionCode { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? ReasonNote { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
