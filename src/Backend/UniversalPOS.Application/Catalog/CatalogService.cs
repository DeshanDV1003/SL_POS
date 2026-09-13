using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Catalog.Dtos;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Catalog;

namespace UniversalPOS.Application.Catalog;

public class CatalogService : ICatalogService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateCategoryRequest> _categoryValidator;
    private readonly IValidator<CreateBrandRequest> _brandValidator;
    private readonly IValidator<CreateUnitRequest> _unitValidator;
    private readonly IValidator<CreateTaxRateRequest> _taxRateValidator;
    private readonly IValidator<CreateProductRequest> _productValidator;

    public CatalogService(
        IApplicationDbContext db,
        IValidator<CreateCategoryRequest> categoryValidator,
        IValidator<CreateBrandRequest> brandValidator,
        IValidator<CreateUnitRequest> unitValidator,
        IValidator<CreateTaxRateRequest> taxRateValidator,
        IValidator<CreateProductRequest> productValidator)
    {
        _db = db;
        _categoryValidator = categoryValidator;
        _brandValidator = brandValidator;
        _unitValidator = unitValidator;
        _taxRateValidator = taxRateValidator;
        _productValidator = productValidator;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.Categories
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                ParentCategoryId = c.ParentCategoryId,
                Name = c.Name,
                DefaultTaxRateId = c.DefaultTaxRateId,
                IsActive = c.IsActive,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateCategoryAsync(long companyId, CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        await _categoryValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _db.Categories.AnyAsync(c => c.CompanyId == companyId && c.Name == request.Name, cancellationToken))
        {
            throw new ConflictException($"A category named '{request.Name}' already exists.");
        }

        var category = new Category
        {
            CompanyId = companyId,
            ParentCategoryId = request.ParentCategoryId,
            Name = request.Name,
            DefaultTaxRateId = request.DefaultTaxRateId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return new CategoryDto { Id = category.Id, ParentCategoryId = category.ParentCategoryId, Name = category.Name, DefaultTaxRateId = category.DefaultTaxRateId, IsActive = category.IsActive };
    }

    public async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.Brands
            .Where(b => b.CompanyId == companyId)
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto { Id = b.Id, Name = b.Name, IsActive = b.IsActive })
            .ToListAsync(cancellationToken);
    }

    public async Task<BrandDto> CreateBrandAsync(long companyId, CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        await _brandValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _db.Brands.AnyAsync(b => b.CompanyId == companyId && b.Name == request.Name, cancellationToken))
        {
            throw new ConflictException($"A brand named '{request.Name}' already exists.");
        }

        var brand = new Brand { CompanyId = companyId, Name = request.Name, CreatedAtUtc = DateTime.UtcNow };
        _db.Brands.Add(brand);
        await _db.SaveChangesAsync(cancellationToken);

        return new BrandDto { Id = brand.Id, Name = brand.Name, IsActive = brand.IsActive };
    }

    public async Task<IReadOnlyList<UnitDto>> GetUnitsAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.Units
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.Name)
            .Select(u => new UnitDto { Id = u.Id, Name = u.Name, Abbreviation = u.Abbreviation, BaseUnitId = u.BaseUnitId, ConversionFactor = u.ConversionFactor })
            .ToListAsync(cancellationToken);
    }

    public async Task<UnitDto> CreateUnitAsync(long companyId, CreateUnitRequest request, CancellationToken cancellationToken = default)
    {
        await _unitValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _db.Units.AnyAsync(u => u.CompanyId == companyId && u.Name == request.Name, cancellationToken))
        {
            throw new ConflictException($"A unit named '{request.Name}' already exists.");
        }

        if (request.BaseUnitId.HasValue &&
            !await _db.Units.AnyAsync(u => u.Id == request.BaseUnitId.Value && u.CompanyId == companyId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Catalog.Unit), request.BaseUnitId.Value);
        }

        var unit = new Domain.Catalog.Unit
        {
            CompanyId = companyId,
            Name = request.Name,
            Abbreviation = request.Abbreviation,
            BaseUnitId = request.BaseUnitId,
            ConversionFactor = request.ConversionFactor,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync(cancellationToken);

        return new UnitDto { Id = unit.Id, Name = unit.Name, Abbreviation = unit.Abbreviation, BaseUnitId = unit.BaseUnitId, ConversionFactor = unit.ConversionFactor };
    }

    public async Task<IReadOnlyList<TaxRateDto>> GetTaxRatesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.TaxRates
            .Where(t => t.CompanyId == companyId)
            .OrderBy(t => t.Name)
            .Select(t => new TaxRateDto
            {
                Id = t.Id,
                Name = t.Name,
                Type = t.Type.ToString(),
                Percentage = t.Percentage,
                IsInclusive = t.IsInclusive,
                EffectiveFromUtc = t.EffectiveFromUtc,
                IsActive = t.IsActive,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TaxRateDto> CreateTaxRateAsync(long companyId, CreateTaxRateRequest request, CancellationToken cancellationToken = default)
    {
        await _taxRateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var taxRate = new TaxRate
        {
            CompanyId = companyId,
            Name = request.Name,
            Type = request.Type,
            Percentage = request.Percentage,
            IsInclusive = request.IsInclusive,
            EffectiveFromUtc = request.EffectiveFromUtc,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.TaxRates.Add(taxRate);
        await _db.SaveChangesAsync(cancellationToken);

        return new TaxRateDto
        {
            Id = taxRate.Id,
            Name = taxRate.Name,
            Type = taxRate.Type.ToString(),
            Percentage = taxRate.Percentage,
            IsInclusive = taxRate.IsInclusive,
            EffectiveFromUtc = taxRate.EffectiveFromUtc,
            IsActive = taxRate.IsActive,
        };
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetProductsAsync(long companyId, string? search, CancellationToken cancellationToken = default)
    {
        var query = _db.Products.Where(p => p.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search));
        }

        return await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductSummaryDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                SellingPrice = p.SellingPrice,
                CostPrice = p.CostPrice,
                CategoryId = p.CategoryId,
                BrandId = p.BrandId,
                IsActive = p.IsActive,
                Barcodes = p.Barcodes.Select(b => b.Barcode).ToList(),
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductSummaryDto?> FindByBarcodeAsync(long companyId, string barcode, CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .Where(p => p.CompanyId == companyId && p.Barcodes.Any(b => b.Barcode == barcode))
            .Select(p => new ProductSummaryDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                SellingPrice = p.SellingPrice,
                CostPrice = p.CostPrice,
                CategoryId = p.CategoryId,
                BrandId = p.BrandId,
                IsActive = p.IsActive,
                Barcodes = p.Barcodes.Select(b => b.Barcode).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProductSummaryDto> CreateProductAsync(long companyId, CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await _productValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _db.Products.AnyAsync(p => p.CompanyId == companyId && p.Sku == request.Sku, cancellationToken))
        {
            throw new ConflictException($"A product with SKU '{request.Sku}' already exists.");
        }

        if (!await _db.Units.AnyAsync(u => u.Id == request.UnitId && u.CompanyId == companyId, cancellationToken))
        {
            throw new NotFoundException(nameof(Domain.Catalog.Unit), request.UnitId);
        }

        foreach (var barcode in request.Barcodes)
        {
            if (await _db.ProductBarcodes.AnyAsync(b => b.Barcode == barcode, cancellationToken))
            {
                throw new ConflictException($"Barcode '{barcode}' is already assigned to another product.");
            }
        }

        var product = new Product
        {
            CompanyId = companyId,
            Sku = request.Sku,
            Name = request.Name,
            Description = request.Description,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            UnitId = request.UnitId,
            TaxRateId = request.TaxRateId,
            CostPrice = request.CostPrice,
            SellingPrice = request.SellingPrice,
            WholesalePrice = request.WholesalePrice,
            MinSellingPrice = request.MinSellingPrice,
            ReorderLevel = request.ReorderLevel,
            MinStock = request.MinStock,
            MaxStock = request.MaxStock,
            TrackBatches = request.TrackBatches,
            TrackExpiry = request.TrackExpiry,
            TrackSerial = request.TrackSerial,
            IsWeighted = request.IsWeighted,
            DefaultKitchenStationId = request.DefaultKitchenStationId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        for (var i = 0; i < request.Barcodes.Count; i++)
        {
            product.Barcodes.Add(new ProductBarcode { Barcode = request.Barcodes[i], IsPrimary = i == 0 });
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        return new ProductSummaryDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            SellingPrice = product.SellingPrice,
            CostPrice = product.CostPrice,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            IsActive = product.IsActive,
            Barcodes = product.Barcodes.Select(b => b.Barcode).ToList(),
        };
    }
}
