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
    /// <returns>The resulting QuantityOnHand after this movement is applied.</returns>
    Task<decimal> PostMovementAsync(
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

    Task<IReadOnlyList<StockReconciliationFlagDto>> GetReconciliationFlagsAsync(long companyId, long branchId, bool openOnly, CancellationToken cancellationToken = default);
    Task<StockReconciliationFlagDto> ResolveReconciliationFlagAsync(long companyId, long flagId, long resolvedByUserId, ResolveStockReconciliationFlagRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockLedgerEntryDto>> GetLedgerAsync(long companyId, long branchId, long productId, CancellationToken cancellationToken = default);

    Task<StockAdjustmentDto> CreateAdjustmentAsync(long companyId, long branchId, long requestedByUserId, CreateStockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<StockAdjustmentDto> ApproveAdjustmentAsync(long companyId, long adjustmentId, long approvedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockAdjustmentDto>> GetPendingAdjustmentsAsync(long companyId, long branchId, CancellationToken cancellationToken = default);

    Task<StockTransferDto> CreateTransferAsync(long companyId, long fromBranchId, long requestedByUserId, CreateStockTransferRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDto> SendTransferAsync(long companyId, long transferId, long sentByUserId, CancellationToken cancellationToken = default);
    Task<StockTransferDto> ReceiveTransferAsync(long companyId, long transferId, long receivedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockTransferDto>> GetTransfersAsync(long companyId, long branchId, CancellationToken cancellationToken = default);

    Task<StockCountDto> CreateCountAsync(long companyId, long branchId, long createdByUserId, CreateStockCountRequest request, CancellationToken cancellationToken = default);
    Task<StockCountDto> SubmitCountLinesAsync(long companyId, long stockCountId, List<SubmitStockCountLineRequest> lines, CancellationToken cancellationToken = default);
    Task<StockCountDto> CompleteCountAsync(long companyId, long stockCountId, long completedByUserId, CancellationToken cancellationToken = default);
}
