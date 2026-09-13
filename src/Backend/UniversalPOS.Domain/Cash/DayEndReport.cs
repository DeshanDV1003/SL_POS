namespace UniversalPOS.Domain.Cash;

public enum DayEndReportStatus
{
    Draft = 0,
    Finalized = 1,
}

/// <summary>
/// The Z-report. While Draft it can be regenerated (business is still trading and
/// totals may change); once Finalized it is locked — regenerating for the same date
/// always returns the finalized row unchanged, protecting it from later tampering.
/// </summary>
public class DayEndReport
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }

    /// <summary>The UTC calendar date this report covers, matched against SaleHeader.CompletedAtUtc.Date — see docs/project-state.md for the documented limitation vs. a configurable business-day cutoff.</summary>
    public DateOnly BusinessDate { get; set; }

    public decimal GrossSales { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal ServiceChargeTotal { get; set; }
    public decimal NetSales { get; set; }
    public decimal RefundTotal { get; set; }

    public decimal CashSalesTotal { get; set; }
    public decimal CardSalesTotal { get; set; }
    public decimal OtherPaymentTotal { get; set; }

    public int TransactionCount { get; set; }
    public int VoidCount { get; set; }

    public DayEndReportStatus Status { get; set; } = DayEndReportStatus.Draft;

    public long GeneratedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
}
