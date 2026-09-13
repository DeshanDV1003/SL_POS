using FluentValidation;
using UniversalPOS.Application.Cash.Dtos;

namespace UniversalPOS.Application.Cash.Validators;

public class OpenShiftRequestValidator : AbstractValidator<OpenShiftRequest>
{
    public OpenShiftRequestValidator()
    {
        RuleFor(x => x.TerminalId).GreaterThan(0);
        RuleFor(x => x.OpeningFloat).GreaterThanOrEqualTo(0);
    }
}

public class RecordCashMovementRequestValidator : AbstractValidator<RecordCashMovementRequest>
{
    public RecordCashMovementRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class CloseShiftRequestValidator : AbstractValidator<CloseShiftRequest>
{
    public CloseShiftRequestValidator()
    {
        RuleFor(x => x.ClosingFloatCounted).GreaterThanOrEqualTo(0);
    }
}
