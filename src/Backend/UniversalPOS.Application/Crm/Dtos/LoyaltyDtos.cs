namespace UniversalPOS.Application.Crm.Dtos;

public class LoyaltyTransactionDto
{
    public long Id { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public int PointsChange { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class RedeemPointsRequest
{
    public int Points { get; set; }
    public string? Reason { get; set; }
}

public class AdjustLoyaltyPointsRequest
{
    public int PointsChange { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class MembershipTierDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinimumPoints { get; set; }
    public decimal PointsMultiplier { get; set; }
}

public class CreateMembershipTierRequest
{
    public string Name { get; set; } = string.Empty;
    public int MinimumPoints { get; set; }
    public decimal PointsMultiplier { get; set; } = 1m;
}
