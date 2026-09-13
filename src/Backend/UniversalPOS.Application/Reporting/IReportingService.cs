using UniversalPOS.Application.Reporting.Dtos;

namespace UniversalPOS.Application.Reporting;

public interface IReportingService
{
    Task<SalesSummaryDto> GetSalesSummaryAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesByProductDto>> GetSalesByProductAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesByCategoryDto>> GetSalesByCategoryAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesByCashierDto>> GetSalesByCashierAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesByPaymentMethodDto>> GetSalesByPaymentMethodAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockValuationDto>> GetStockValuationAsync(long companyId, long branchId, CancellationToken cancellationToken = default);

    Task<DashboardSummaryDto> GetDashboardSummaryAsync(long companyId, long branchId, CancellationToken cancellationToken = default);
}
