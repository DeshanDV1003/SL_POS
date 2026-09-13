namespace UniversalPOS.Domain.Identity;

/// <summary>Which branches a user may operate in. Composite key (UserId, BranchId).</summary>
public class UserBranch
{
    public long UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public long BranchId { get; set; }
}
