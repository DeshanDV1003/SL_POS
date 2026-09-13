using FluentValidation;
using UniversalPOS.Application.Catalog.Dtos;

namespace UniversalPOS.Application.Catalog.Validators;

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class CreateBrandRequestValidator : AbstractValidator<CreateBrandRequest>
{
    public CreateBrandRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class CreateUnitRequestValidator : AbstractValidator<CreateUnitRequest>
{
    public CreateUnitRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Abbreviation).NotEmpty().MaximumLength(10);
        RuleFor(x => x.ConversionFactor).GreaterThan(0);
    }
}

public class CreateTaxRateRequestValidator : AbstractValidator<CreateTaxRateRequest>
{
    public CreateTaxRateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UnitId).GreaterThan(0);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxStock).GreaterThanOrEqualTo(x => x.MinStock)
            .WithMessage("MaxStock must be greater than or equal to MinStock.");
        RuleFor(x => x.MinSellingPrice)
            .LessThanOrEqualTo(x => x.SellingPrice)
            .When(x => x.MinSellingPrice.HasValue)
            .WithMessage("MinSellingPrice cannot exceed SellingPrice.");
        RuleForEach(x => x.Barcodes).NotEmpty().MaximumLength(64);
    }
}
