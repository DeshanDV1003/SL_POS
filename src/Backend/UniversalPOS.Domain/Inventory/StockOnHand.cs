namespace UniversalPOS.Domain.Inventory;

/// <summary>
/// A derived, transactionally-maintained summary of current stock per Branch+Product —
/// every write to this table happens in the same transaction as the StockLedger row
/// that caused it, so the two can never drift. RowVersion guards against two terminals
/// racing to update the same product's stock concurrently.
/// </summary>
public class StockOnHand
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public long BranchId { get; set; }
    public long ProductId { get; set; }

    public decimal QuantityOnHand { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
