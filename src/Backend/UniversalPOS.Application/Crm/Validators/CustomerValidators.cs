using FluentValidation;
using UniversalPOS.Application.Crm.Dtos;

namespace UniversalPOS.Application.Crm.Validators;

public class CreateCustomerGroupRequestValidator : AbstractValidator<CreateCustomerGroupRequest>
{
    public CreateCustomerGroupRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DefaultDiscountPercentage).InclusiveBetween(0, 100);
    }
}

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}
