using UniversalPOS.Application.Crm.Dtos;

namespace UniversalPOS.Application.Crm;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerGroupDto>> GetCustomerGroupsAsync(long companyId, CancellationToken cancellationToken = default);
    Task<CustomerGroupDto> CreateCustomerGroupAsync(long companyId, CreateCustomerGroupRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDto>> GetCustomersAsync(long companyId, string? search, CancellationToken cancellationToken = default);
    Task<CustomerDto> CreateCustomerAsync(long companyId, CreateCustomerRequest request, CancellationToken cancellationToken = default);
}
