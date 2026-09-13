using FluentValidation;
using UniversalPOS.Application.Inventory.Dtos;

namespace UniversalPOS.Application.Inventory.Validators;

public class CreateStockTransferRequestValidator : AbstractValidator<CreateStockTransferRequest>
{
    public CreateStockTransferRequestValidator()
    {
        RuleFor(x => x.ToBranchId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

public class SubmitStockCountLineRequestValidator : AbstractValidator<SubmitStockCountLineRequest>
{
    public SubmitStockCountLineRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.CountedQuantity).GreaterThanOrEqualTo(0);
    }
}
