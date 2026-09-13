using UniversalPOS.Application.Sales.Dtos;

namespace UniversalPOS.Application.Sales;

public interface ISalesService
{
    Task<SaleReceiptDto> CheckoutAsync(long companyId, long branchId, long cashierUserId, CreateSaleRequest request, CancellationToken cancellationToken = default);
    Task<SaleReceiptDto> GetSaleAsync(long companyId, long saleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleReceiptDto>> GetSalesAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
    Task<SaleReceiptDto> VoidSaleAsync(long companyId, long branchId, long? terminalId, long userId, long saleId, VoidSaleRequest request, CancellationToken cancellationToken = default);

    Task<HeldBillDto> HoldAsync(long companyId, long branchId, long cashierUserId, CreateHeldBillRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HeldBillDto>> GetHeldBillsAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
    Task<HeldBillDto> RecallAsync(long companyId, long heldBillId, CancellationToken cancellationToken = default);
    Task DeleteHeldBillAsync(long companyId, long heldBillId, CancellationToken cancellationToken = default);
}
