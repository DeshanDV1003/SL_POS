using UniversalPOS.Application.Organization.Dtos;

namespace UniversalPOS.Application.Organization;

public interface IOrganizationQueryService
{
    Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(CancellationToken cancellationToken = default);
    Task<CompanyDto> UpdateCompanyLoyaltySettingsAsync(long companyId, UpdateCompanyLoyaltySettingsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchDto>> GetBranchesAsync(long companyId, CancellationToken cancellationToken = default);
    Task<BranchDto> UpdateBranchCashSettingsAsync(long companyId, long branchId, UpdateBranchCashSettingsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TerminalDto>> GetTerminalsAsync(long branchId, CancellationToken cancellationToken = default);

    /// <summary>Records that a terminal is online right now — called on login and periodically while connected, so an offline-queue UI can tell "still syncing" apart from "silently stuck."</summary>
    Task RecordTerminalHeartbeatAsync(long terminalId, CancellationToken cancellationToken = default);
}
