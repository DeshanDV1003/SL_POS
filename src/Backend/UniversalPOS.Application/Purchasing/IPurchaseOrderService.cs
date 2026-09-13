using UniversalPOS.Application.Purchasing.Dtos;

namespace UniversalPOS.Application.Purchasing;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderDto> CreateAsync(long companyId, long branchId, long createdByUserId, CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> ApproveAsync(long companyId, long purchaseOrderId, long approvedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrderDto>> GetAsync(long companyId, long branchId, CancellationToken cancellationToken = default);

    Task<GoodsReceivedNoteDto> ReceiveGoodsAsync(long companyId, long branchId, long receivedByUserId, ReceiveGoodsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoodsReceivedNoteDto>> GetGoodsReceivedNotesAsync(long companyId, long branchId, CancellationToken cancellationToken = default);

    Task<PurchaseInvoiceDto> CreateInvoiceAsync(long companyId, long branchId, CreatePurchaseInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceDto>> GetInvoicesAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto> RecordPaymentAsync(long companyId, long invoiceId, long userId, RecordSupplierPaymentRequest request, CancellationToken cancellationToken = default);
}
