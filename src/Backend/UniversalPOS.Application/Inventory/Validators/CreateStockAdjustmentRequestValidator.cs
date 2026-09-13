using FluentValidation;
using UniversalPOS.Application.Inventory.Dtos;

namespace UniversalPOS.Application.Inventory.Validators;

public class CreateStockAdjustmentRequestValidator : AbstractValidator<CreateStockAdjustmentRequest>
{
    public CreateStockAdjustmentRequestValidator()
    {
        RuleFor(x => x.Lines).NotEmpty().WithMessage("At least one line is required.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.QuantityChange).NotEqual(0).WithMessage("QuantityChange cannot be zero.");
        });
    }
}
