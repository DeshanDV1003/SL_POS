namespace UniversalPOS.Domain.Sales;

/// <summary>A suspended cart, distinct from a finalized SaleHeader — no stock or financial record is created until recalled and checked out.</summary>
public class HeldBill
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long TerminalId { get; set; }
    public long CashierUserId { get; set; }
    public long? CustomerId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<HeldBillLine> Lines { get; set; } = new List<HeldBillLine>();
}
