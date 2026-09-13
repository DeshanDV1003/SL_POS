using UniversalPOS.Application.Crm.Dtos;

namespace UniversalPOS.Application.Crm;

public interface ILoyaltyService
{
    /// <summary>Best-effort points earn at checkout — not part of the public API surface directly, called by ISalesService.</summary>
    Task EarnPointsForSaleAsync(long companyId, long customerId, long saleHeaderId, decimal grandTotal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoyaltyTransactionDto>> GetHistoryAsync(long companyId, long customerId, CancellationToken cancellationToken = default);
    Task RedeemPointsAsync(long companyId, long customerId, RedeemPointsRequest request, CancellationToken cancellationToken = default);
    Task AdjustPointsAsync(long companyId, long customerId, long adjustedByUserId, AdjustLoyaltyPointsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipTierDto>> GetTiersAsync(long companyId, CancellationToken cancellationToken = default);
    Task<MembershipTierDto> CreateTierAsync(long companyId, CreateMembershipTierRequest request, CancellationToken cancellationToken = default);
}
