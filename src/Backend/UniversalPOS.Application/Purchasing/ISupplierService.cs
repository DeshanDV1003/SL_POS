using UniversalPOS.Application.Purchasing.Dtos;

namespace UniversalPOS.Application.Purchasing;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> GetSuppliersAsync(long companyId, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateSupplierAsync(long companyId, CreateSupplierRequest request, CancellationToken cancellationToken = default);
}
