using FluentValidation;
using UniversalPOS.Application.Crm.Dtos;

namespace UniversalPOS.Application.Crm.Validators;

public class RedeemPointsRequestValidator : AbstractValidator<RedeemPointsRequest>
{
    public RedeemPointsRequestValidator()
    {
        RuleFor(x => x.Points).GreaterThan(0);
    }
}

public class AdjustLoyaltyPointsRequestValidator : AbstractValidator<AdjustLoyaltyPointsRequest>
{
    public AdjustLoyaltyPointsRequestValidator()
    {
        RuleFor(x => x.PointsChange).NotEqual(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

public class CreateMembershipTierRequestValidator : AbstractValidator<CreateMembershipTierRequest>
{
    public CreateMembershipTierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MinimumPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PointsMultiplier).GreaterThan(0);
    }
}
