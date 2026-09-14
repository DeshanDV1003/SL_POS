using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Sales.Dtos;

public class CreatePromotionRequest
{
    public string Name { get; set; } = string.Empty;
    public PromotionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public PromotionScope Scope { get; set; } = PromotionScope.AllProducts;
    public long? CategoryId { get; set; }
    public long? ProductId { get; set; }
    public decimal MinQuantity { get; set; } = 1;
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public int Priority { get; set; }
}

public class PromotionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public string Scope { get; set; } = string.Empty;
    public long? CategoryId { get; set; }
    public long? ProductId { get; set; }
    public decimal MinQuantity { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
}

public class CreateCouponRequest
{
    public string Code { get; set; } = string.Empty;
    public PromotionDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinSaleAmount { get; set; }
    public int? MaxRedemptions { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public class CouponDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal MinSaleAmount { get; set; }
    public int? MaxRedemptions { get; set; }
    public int TimesRedeemed { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; }
}
