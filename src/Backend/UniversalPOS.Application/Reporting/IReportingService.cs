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

    /// <summary>Daily net-sales/transaction-count buckets over the range — the raw series behind a revenue-trend chart.</summary>
    Task<IReadOnlyList<SalesTrendPointDto>> GetSalesTrendAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Same-range sales summary side by side for every branch in the company — the data behind a branch-comparison chart.</summary>
    Task<IReadOnlyList<BranchComparisonDto>> GetBranchComparisonAsync(long companyId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<DiscountReportDto> GetDiscountReportAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<TaxReportDto> GetTaxReportAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitchenStationPerformanceDto>> GetKitchenPerformanceAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WaiterPerformanceDto>> GetWaiterPerformanceAsync(long companyId, long branchId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Products with stock on hand that haven't sold in at least staleAfterDays (default 30).</summary>
    Task<IReadOnlyList<SlowMovingStockDto>> GetSlowMovingStockAsync(long companyId, long branchId, int staleAfterDays = 30, CancellationToken cancellationToken = default);
}
