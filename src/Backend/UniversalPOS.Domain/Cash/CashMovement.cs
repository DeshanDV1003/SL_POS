namespace UniversalPOS.Domain.Cash;

public enum CashMovementType
{
    CashIn = 0,
    CashOut = 1,
    Petty = 2,
}

/// <summary>An out-of-sale cash movement within a shift — a bank drop, a till top-up, petty cash for a supplier COD delivery, etc.</summary>
public class CashMovement
{
    public long Id { get; set; }
    public long CashierShiftId { get; set; }
    public CashierShift CashierShift { get; set; } = null!;

    public CashMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }

    public long CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
