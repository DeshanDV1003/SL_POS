using UniversalPOS.Application.Crm.Dtos;

namespace UniversalPOS.Application.Crm;

public interface ILoyaltyService
{
    /// <summary>Best-effort points earn at checkout — not part of the public API surface directly, called by ISalesService.</summary>
    Task EarnPointsForSaleAsync(long companyId, long customerId, long saleHeaderId, decimal grandTotal, CancellationToken cancellationToken = default);

    /// <summary>Redeems points as part of paying for a sale (PaymentMethod.LoyaltyPoints) — called by ISalesService inside the same checkout transaction, after the sale row exists so the ledger row can reference it.</summary>
    Task RedeemPointsForSaleAsync(long companyId, long customerId, int points, long saleHeaderId, CancellationToken cancellationToken = default);

    /// <summary>How many whole points the customer's balance covers a given currency amount for, and whether they have enough — used by checkout to validate a LoyaltyPoints payment line before authorizing anything.</summary>
    Task<(int PointsRequired, bool HasEnoughBalance)> QuotePointsForAmountAsync(long companyId, long customerId, decimal amount, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoyaltyTransactionDto>> GetHistoryAsync(long companyId, long customerId, CancellationToken cancellationToken = default);
    Task RedeemPointsAsync(long companyId, long customerId, RedeemPointsRequest request, CancellationToken cancellationToken = default);
    Task AdjustPointsAsync(long companyId, long customerId, long adjustedByUserId, AdjustLoyaltyPointsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MembershipTierDto>> GetTiersAsync(long companyId, CancellationToken cancellationToken = default);
    Task<MembershipTierDto> CreateTierAsync(long companyId, CreateMembershipTierRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires Earned point batches past their ExpiresAtUtc for every customer in the
    /// company. v1 simplification: expires at the customer-balance level (capped at
    /// their current balance) rather than tracking per-batch remaining points through
    /// FIFO redemption consumption — see docs/project-state.md. Returns how many
    /// customers had points expired.
    /// </summary>
    Task<int> ExpirePointsAsync(long companyId, CancellationToken cancellationToken = default);
}
