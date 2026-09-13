using UniversalPOS.Application.Inventory.Dtos;
using UniversalPOS.Domain.Inventory;

namespace UniversalPOS.Application.Inventory;

public interface IStockService
{
    /// <summary>
    /// Adds a StockLedger row and updates the StockOnHand summary for one product, both
    /// tracked on the current DbContext but NOT saved — the caller controls the
    /// transaction boundary and must call SaveChangesAsync once for the whole business
    /// operation (e.g. all lines of one GRN, or a whole sale), so a partial failure
    /// never leaves the ledger and the summary out of sync.
    /// </summary>
    Task PostMovementAsync(
        long companyId,
        long branchId,
        long productId,
        StockMovementType movementType,
        decimal quantityChange,
        string referenceType,
        long referenceId,
        long? userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockOnHandDto>> GetStockOnHandAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockLedgerEntryDto>> GetLedgerAsync(long companyId, long branchId, long productId, CancellationToken cancellationToken = default);

    Task<StockAdjustmentDto> CreateAdjustmentAsync(long companyId, long branchId, long requestedByUserId, CreateStockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<StockAdjustmentDto> ApproveAdjustmentAsync(long companyId, long adjustmentId, long approvedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockAdjustmentDto>> GetPendingAdjustmentsAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
}
