using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
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
                RequireOpenShiftForSale = b.RequireOpenShiftForSale,
                BusinessDayCutoffHour = b.BusinessDayCutoffHour,
                IsActive = b.IsActive,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<BranchDto> UpdateBranchCashSettingsAsync(long companyId, long branchId, UpdateBranchCashSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.BusinessDayCutoffHour is < 0 or > 23)
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                [nameof(request.BusinessDayCutoffHour)] = new[] { "Must be between 0 and 23." },
            });
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException("Branch", branchId);

        branch.RequireOpenShiftForSale = request.RequireOpenShiftForSale;
        branch.BusinessDayCutoffHour = request.BusinessDayCutoffHour;
        await _db.SaveChangesAsync(cancellationToken);

        return new BranchDto
        {
            Id = branch.Id,
            CompanyId = branch.CompanyId,
            Name = branch.Name,
            Code = branch.Code,
            City = branch.City,
            BusinessTypeFlags = branch.BusinessTypeFlags.ToString(),
            RequireOpenShiftForSale = branch.RequireOpenShiftForSale,
            BusinessDayCutoffHour = branch.BusinessDayCutoffHour,
            IsActive = branch.IsActive,
        };
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
