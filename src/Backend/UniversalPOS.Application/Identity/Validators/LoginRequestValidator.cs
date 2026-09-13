using FluentValidation;
using UniversalPOS.Application.Identity.Dtos;

namespace UniversalPOS.Application.Identity.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(200);
    }
}
