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

public class CancelTicketRequestValidator : AbstractValidator<CancelTicketRequest>
{
    public CancelTicketRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

public class CreateStandaloneOrderRequestValidator : AbstractValidator<CreateStandaloneOrderRequest>
{
    public CreateStandaloneOrderRequestValidator()
    {
        RuleFor(x => x.DeliveryAddress).NotEmpty()
            .When(x => x.OrderType == UniversalPOS.Domain.Restaurant.OrderType.Delivery)
            .WithMessage("A delivery order requires a delivery address.");
    }
}
