using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales;

public class PromotionEngine : IPromotionEngine
{
    private readonly IApplicationDbContext _db;

    public PromotionEngine(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetEffectiveDiscountPercentageAsync(
        long companyId,
        long productId,
        long? categoryId,
        decimal quantity,
        decimal grossAmount,
        decimal manualDiscountPercentage,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var candidates = await _db.Promotions
            .Where(p => p.CompanyId == companyId && p.IsActive
                && p.StartDateUtc <= now && (p.EndDateUtc == null || p.EndDateUtc >= now)
                && p.MinQuantity <= quantity
                && (p.Scope == PromotionScope.AllProducts
                    || (p.Scope == PromotionScope.Category && p.CategoryId == categoryId)
                    || (p.Scope == PromotionScope.Product && p.ProductId == productId)))
            .OrderByDescending(p => p.Priority)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return manualDiscountPercentage;
        }

        // Most-specific scope wins among equal priority: Product > Category > AllProducts.
        var best = candidates
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Scope == PromotionScope.Product ? 0 : p.Scope == PromotionScope.Category ? 1 : 2)
            .First();

        var promotionPercentage = best.DiscountType == PromotionDiscountType.Percentage
            ? best.DiscountValue
            : grossAmount > 0 ? Math.Min(100m, best.DiscountValue / grossAmount * 100m) : 0m;

        return Math.Max(manualDiscountPercentage, promotionPercentage);
    }
}
