using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Sales.Dtos;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales;

public class PromotionService : IPromotionService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreatePromotionRequest> _validator;
    private readonly IValidator<CreateCouponRequest> _couponValidator;

    public PromotionService(IApplicationDbContext db, IValidator<CreatePromotionRequest> validator, IValidator<CreateCouponRequest> couponValidator)
    {
        _db = db;
        _validator = validator;
        _couponValidator = couponValidator;
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

    public async Task<IReadOnlyList<CouponDto>> GetCouponsAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.Coupons
            .Where(c => c.CompanyId == companyId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => ToDto(c))
            .ToListAsync(cancellationToken);
    }

    public async Task<CouponDto> CreateCouponAsync(long companyId, CreateCouponRequest request, CancellationToken cancellationToken = default)
    {
        await _couponValidator.ValidateAndThrowAsync(request, cancellationToken);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Coupons.AnyAsync(c => c.CompanyId == companyId && c.Code == code, cancellationToken))
        {
            throw new ConflictException($"A coupon with code '{code}' already exists.");
        }

        var coupon = new Coupon
        {
            CompanyId = companyId,
            Code = code,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MinSaleAmount = request.MinSaleAmount,
            MaxRedemptions = request.MaxRedemptions,
            ExpiresAtUtc = request.ExpiresAtUtc,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(coupon);
    }

    private static CouponDto ToDto(Coupon c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        DiscountType = c.DiscountType.ToString(),
        DiscountValue = c.DiscountValue,
        MinSaleAmount = c.MinSaleAmount,
        MaxRedemptions = c.MaxRedemptions,
        TimesRedeemed = c.TimesRedeemed,
        ExpiresAtUtc = c.ExpiresAtUtc,
        IsActive = c.IsActive,
    };

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
