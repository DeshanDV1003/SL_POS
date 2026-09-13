using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Organization.Dtos;

namespace UniversalPOS.Application.Organization;

public class OrganizationQueryService : IOrganizationQueryService
{
    private readonly IApplicationDbContext _db;

    public OrganizationQueryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Companies
            .OrderBy(c => c.Name)
            .Select(c => new CompanyDto
            {
                Id = c.Id,
                Name = c.Name,
                LegalName = c.LegalName,
                DefaultCurrencyCode = c.DefaultCurrencyCode,
                IsVatRegistered = c.IsVatRegistered,
                IsActive = c.IsActive,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BranchDto>> GetBranchesAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.Branches
            .Where(b => b.CompanyId == companyId)
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto
            {
                Id = b.Id,
                CompanyId = b.CompanyId,
                Name = b.Name,
                Code = b.Code,
                City = b.City,
                BusinessTypeFlags = b.BusinessTypeFlags.ToString(),
                IsActive = b.IsActive,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TerminalDto>> GetTerminalsAsync(long branchId, CancellationToken cancellationToken = default)
    {
        return await _db.Terminals
            .Where(t => t.BranchId == branchId)
            .OrderBy(t => t.Name)
            .Select(t => new TerminalDto
            {
                Id = t.Id,
                BranchId = t.BranchId,
                Name = t.Name,
                Code = t.Code,
                IsActive = t.IsActive,
                LastSeenAtUtc = t.LastSeenAtUtc,
            })
            .ToListAsync(cancellationToken);
    }
}
