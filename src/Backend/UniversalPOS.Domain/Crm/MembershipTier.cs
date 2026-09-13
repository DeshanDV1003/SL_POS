namespace UniversalPOS.Domain.Crm;

/// <summary>e.g. Silver/Gold/Platinum. A customer's tier is derived from their current loyalty point balance, not manually assigned.</summary>
public class MembershipTier
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Minimum current point balance required to hold this tier.</summary>
    public int MinimumPoints { get; set; }

    /// <summary>Perk: extra earn rate multiplier while at this tier, e.g. 1.5 = 50% bonus points.</summary>
    public decimal PointsMultiplier { get; set; } = 1m;

    public bool IsActive { get; set; } = true;
}
