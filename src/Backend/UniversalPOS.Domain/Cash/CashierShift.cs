namespace UniversalPOS.Domain.Cash;

public enum ShiftStatus
{
    Open = 0,
    Closed = 1,
}

/// <summary>
/// A cashier's session at a terminal. Closing computes ExpectedCash from the opening
/// float, cash sales during the shift, and CashMovements — never guessed, always
/// derived from the same ledgers everything else in the system uses.
/// </summary>
public class CashierShift
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long TerminalId { get; set; }
    public long CashierUserId { get; set; }

    public decimal OpeningFloat { get; set; }
    public decimal? ClosingFloatCounted { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? VarianceAmount { get; set; }

    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public ICollection<CashMovement> Movements { get; set; } = new List<CashMovement>();
}
