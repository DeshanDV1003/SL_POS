using FluentValidation;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales.Validators;

public class CreatePromotionRequestValidator : AbstractValidator<CreatePromotionRequest>
{
    public CreatePromotionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100).When(x => x.DiscountType == PromotionDiscountType.Percentage)
            .WithMessage("A percentage promotion's DiscountValue cannot exceed 100.");
        RuleFor(x => x.MinQuantity).GreaterThan(0);
        RuleFor(x => x.CategoryId).NotNull().When(x => x.Scope == PromotionScope.Category)
            .WithMessage("CategoryId is required when Scope is Category.");
        RuleFor(x => x.ProductId).NotNull().When(x => x.Scope == PromotionScope.Product)
            .WithMessage("ProductId is required when Scope is Product.");
        RuleFor(x => x.EndDateUtc).GreaterThan(x => x.StartDateUtc).When(x => x.EndDateUtc.HasValue);
    }
}

public class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DiscountValue).GreaterThan(0);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100).When(x => x.DiscountType == PromotionDiscountType.Percentage)
            .WithMessage("A percentage coupon's DiscountValue cannot exceed 100.");
        RuleFor(x => x.MinSaleAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxRedemptions).GreaterThan(0).When(x => x.MaxRedemptions.HasValue);
    }
}
