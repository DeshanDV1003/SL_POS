using UniversalPOS.Application.Organization.Dtos;

namespace UniversalPOS.Application.Organization;

public interface IOrganizationQueryService
{
    Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(long companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TerminalDto>> GetTerminalsAsync(long branchId, CancellationToken cancellationToken = default);
}
