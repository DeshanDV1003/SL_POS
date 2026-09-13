using FluentValidation;
using UniversalPOS.Application.Sales.Dtos;

namespace UniversalPOS.Application.Sales.Validators;

public class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator()
    {
        RuleFor(x => x.TerminalId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A sale must have at least one line.");
        RuleFor(x => x.Payments).NotEmpty().WithMessage("A sale must have at least one payment.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.DiscountPercentage).InclusiveBetween(0, 100);
        });

        RuleForEach(x => x.Payments).ChildRules(payment =>
        {
            payment.RuleFor(p => p.Amount).GreaterThan(0);
        });

        RuleFor(x => x.PurchaserTin)
            .NotEmpty()
            .When(x => x.InvoiceMode == Domain.Sales.InvoiceMode.FullTaxInvoice)
            .WithMessage("A full tax invoice requires the purchaser's TIN.");
    }
}

public class CreateHeldBillRequestValidator : AbstractValidator<CreateHeldBillRequest>
{
    public CreateHeldBillRequestValidator()
    {
        RuleFor(x => x.TerminalId).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

public class VoidSaleRequestValidator : AbstractValidator<VoidSaleRequest>
{
    public VoidSaleRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}
