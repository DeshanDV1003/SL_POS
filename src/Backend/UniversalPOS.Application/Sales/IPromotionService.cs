using UniversalPOS.Application.Sales.Dtos;

namespace UniversalPOS.Application.Sales;

public interface IPromotionService
{
    Task<IReadOnlyList<PromotionDto>> GetPromotionsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<PromotionDto> CreatePromotionAsync(long companyId, CreatePromotionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CouponDto>> GetCouponsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<CouponDto> CreateCouponAsync(long companyId, CreateCouponRequest request, CancellationToken cancellationToken = default);
}
