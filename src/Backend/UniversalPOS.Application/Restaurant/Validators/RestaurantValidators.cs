using FluentValidation;
using UniversalPOS.Application.Restaurant.Dtos;

namespace UniversalPOS.Application.Restaurant.Validators;

public class AddOrderLineRequestValidator : AbstractValidator<AddOrderLineRequest>
{
    public AddOrderLineRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
