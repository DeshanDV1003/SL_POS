namespace UniversalPOS.Domain.Inventory;

public enum StockTransferStatus
{
    Requested = 0,
    Sent = 1,
    Received = 2,
    Cancelled = 3,
}

/// <summary>
/// Branch-to-branch stock movement. Stock leaves FromBranch only when Sent (posts a
/// TransferOut movement there) and arrives at ToBranch only when Received (posts a
/// TransferIn movement there) — a Requested transfer touches no stock at all.
/// </summary>
public class StockTransfer
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long FromBranchId { get; set; }
    public long ToBranchId { get; set; }

    public StockTransferStatus Status { get; set; } = StockTransferStatus.Requested;
    public long RequestedByUserId { get; set; }
    public long? SentByUserId { get; set; }
    public long? ReceivedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }

    public ICollection<StockTransferLine> Lines { get; set; } = new List<StockTransferLine>();
}
