namespace UniversalPOS.Application.Sales;

public interface IPromotionEngine
{
    /// <summary>
    /// Returns the discount percentage to apply to a line — whichever is larger of the
    /// cashier's manual discount and the best-matching active promotion (never
    /// stacked). Fixed-amount promotions are converted to an equivalent percentage of
    /// the line's gross amount so the caller's math (SaleLineCalculator) only ever
    /// deals in percentages.
    /// </summary>
    Task<decimal> GetEffectiveDiscountPercentageAsync(
        long companyId,
        long productId,
        long? categoryId,
        decimal quantity,
        decimal grossAmount,
        decimal manualDiscountPercentage,
        CancellationToken cancellationToken = default);
}
