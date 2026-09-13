using UniversalPOS.Application.Catalog.Dtos;

namespace UniversalPOS.Application.Catalog;

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(long companyId, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateCategoryAsync(long companyId, CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BrandDto>> GetBrandsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<BrandDto> CreateBrandAsync(long companyId, CreateBrandRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnitDto>> GetUnitsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<UnitDto> CreateUnitAsync(long companyId, CreateUnitRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaxRateDto>> GetTaxRatesAsync(long companyId, CancellationToken cancellationToken = default);
    Task<TaxRateDto> CreateTaxRateAsync(long companyId, CreateTaxRateRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductSummaryDto>> GetProductsAsync(long companyId, string? search, CancellationToken cancellationToken = default);
    Task<ProductSummaryDto> CreateProductAsync(long companyId, CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductSummaryDto?> FindByBarcodeAsync(long companyId, string barcode, CancellationToken cancellationToken = default);
}
