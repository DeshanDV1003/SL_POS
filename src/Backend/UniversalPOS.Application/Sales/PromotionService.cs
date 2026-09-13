using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales;

public class PromotionService : IPromotionService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreatePromotionRequest> _validator;

    public PromotionService(IApplicationDbContext db, IValidator<CreatePromotionRequest> validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<IReadOnlyList<PromotionDto>> GetPromotionsAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.Promotions
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.Priority)
            .Select(p => ToDto(p))
            .ToListAsync(cancellationToken);
    }

    public async Task<PromotionDto> CreatePromotionAsync(long companyId, CreatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var promotion = new Promotion
        {
            CompanyId = companyId,
            Name = request.Name,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            Scope = request.Scope,
            CategoryId = request.CategoryId,
            ProductId = request.ProductId,
            MinQuantity = request.MinQuantity,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            Priority = request.Priority,
            IsActive = true,
        };
        _db.Promotions.Add(promotion);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(promotion);
    }

    private static PromotionDto ToDto(Promotion p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        DiscountType = p.DiscountType.ToString(),
        DiscountValue = p.DiscountValue,
        Scope = p.Scope.ToString(),
        CategoryId = p.CategoryId,
        ProductId = p.ProductId,
        MinQuantity = p.MinQuantity,
        StartDateUtc = p.StartDateUtc,
        EndDateUtc = p.EndDateUtc,
        IsActive = p.IsActive,
        Priority = p.Priority,
    };
}
