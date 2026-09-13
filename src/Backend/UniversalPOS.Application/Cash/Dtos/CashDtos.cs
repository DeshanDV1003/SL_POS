using UniversalPOS.Domain.Cash;

namespace UniversalPOS.Application.Cash.Dtos;

public class OpenShiftRequest
{
    public long TerminalId { get; set; }
    public decimal OpeningFloat { get; set; }
}

public class ShiftDto
{
    public long Id { get; set; }
    public long TerminalId { get; set; }
    public long CashierUserId { get; set; }
    public decimal OpeningFloat { get; set; }
    public decimal? ClosingFloatCounted { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? VarianceAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}

public class RecordCashMovementRequest
{
    public CashMovementType MovementType { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}

public class CloseShiftRequest
{
    public decimal ClosingFloatCounted { get; set; }
}

public class DayEndReportDto
{
    public long Id { get; set; }
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
    public string Status { get; set; } = string.Empty;
}
