namespace UniversalPOS.Domain.Crm;

public enum LoyaltyTransactionType
{
    Earned = 0,
    Redeemed = 1,
    ManualAdjustment = 2,
    Expired = 3,
    Promotional = 4,
}

/// <summary>
/// Immutable, append-only — Customer.LoyaltyPointsBalance is a derived summary updated
/// only alongside a row here, exactly like StockLedger/StockOnHand. A manual
/// adjustment is never applied without one of these rows recording who did it and why.
/// </summary>
public class LoyaltyTransaction
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long CustomerId { get; set; }

    public LoyaltyTransactionType TransactionType { get; set; }

    /// <summary>Signed: positive for points earned/credited, negative for redeemed/expired.</summary>
    public int PointsChange { get; set; }

    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public string? Notes { get; set; }

    public long? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
