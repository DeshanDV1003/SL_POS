using UniversalPOS.Application.Sales.Dtos;

namespace UniversalPOS.Application.Sales;

public interface IPromotionService
{
    Task<IReadOnlyList<PromotionDto>> GetPromotionsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<PromotionDto> CreatePromotionAsync(long companyId, CreatePromotionRequest request, CancellationToken cancellationToken = default);
}
