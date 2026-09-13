using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Crm.Dtos;
using UniversalPOS.Domain.Auditing;
using UniversalPOS.Domain.Crm;

namespace UniversalPOS.Application.Crm;

public class LoyaltyService : ILoyaltyService
{
    /// <summary>
    /// Default earn rate: 1 point per LKR 100 spent. Not yet per-company configurable
    /// — see docs/project-state.md. Multiplied by the customer's current membership
    /// tier's PointsMultiplier, if any.
    /// </summary>
    private const decimal PointsPerCurrencyUnit = 1m / 100m;

    private readonly IApplicationDbContext _db;
    private readonly IValidator<RedeemPointsRequest> _redeemValidator;
    private readonly IValidator<AdjustLoyaltyPointsRequest> _adjustValidator;
    private readonly IValidator<CreateMembershipTierRequest> _tierValidator;

    public LoyaltyService(
        IApplicationDbContext db,
        IValidator<RedeemPointsRequest> redeemValidator,
        IValidator<AdjustLoyaltyPointsRequest> adjustValidator,
        IValidator<CreateMembershipTierRequest> tierValidator)
    {
        _db = db;
        _redeemValidator = redeemValidator;
        _adjustValidator = adjustValidator;
        _tierValidator = tierValidator;
    }

    public async Task EarnPointsForSaleAsync(long companyId, long customerId, long saleHeaderId, decimal grandTotal, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId, cancellationToken);
        if (customer is null)
        {
            return; // best-effort: an invalid customer id should not fail the sale that already completed.
        }

        var multiplier = 1m;
        if (customer.MembershipTierId.HasValue)
        {
            multiplier = await _db.MembershipTiers
                .Where(t => t.Id == customer.MembershipTierId.Value)
                .Select(t => t.PointsMultiplier)
                .FirstOrDefaultAsync(cancellationToken);
            if (multiplier == 0) multiplier = 1m;
        }

        var points = (int)Math.Floor(grandTotal * PointsPerCurrencyUnit * multiplier);
        if (points <= 0)
        {
            return;
        }

        await PostLoyaltyTransactionAsync(customer, LoyaltyTransactionType.Earned, points, nameof(Domain.Sales.SaleHeader), saleHeaderId, null, null, cancellationToken);
    }

    public async Task<IReadOnlyList<LoyaltyTransactionDto>> GetHistoryAsync(long companyId, long customerId, CancellationToken cancellationToken = default)
    {
        return await _db.LoyaltyTransactions
            .Where(t => t.CompanyId == companyId && t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new LoyaltyTransactionDto
            {
                Id = t.Id,
                TransactionType = t.TransactionType.ToString(),
                PointsChange = t.PointsChange,
                Notes = t.Notes,
                CreatedAtUtc = t.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task RedeemPointsAsync(long companyId, long customerId, RedeemPointsRequest request, CancellationToken cancellationToken = default)
    {
        await _redeemValidator.ValidateAndThrowAsync(request, cancellationToken);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), customerId);

        if (customer.LoyaltyPointsBalance < request.Points)
        {
            throw new ConflictException($"Customer has only {customer.LoyaltyPointsBalance} points, cannot redeem {request.Points}.");
        }

        await PostLoyaltyTransactionAsync(customer, LoyaltyTransactionType.Redeemed, -request.Points, null, null, request.Reason, null, cancellationToken);
    }

    public async Task AdjustPointsAsync(long companyId, long customerId, long adjustedByUserId, AdjustLoyaltyPointsRequest request, CancellationToken cancellationToken = default)
    {
        await _adjustValidator.ValidateAndThrowAsync(request, cancellationToken);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), customerId);

        if (customer.LoyaltyPointsBalance + request.PointsChange < 0)
        {
            throw new ConflictException("This adjustment would make the customer's loyalty balance negative.");
        }

        var before = customer.LoyaltyPointsBalance;

        await _db.ExecuteInTransactionAsync(async () =>
        {
            await PostLoyaltyTransactionAsync(customer, LoyaltyTransactionType.ManualAdjustment, request.PointsChange, null, null, request.Reason, adjustedByUserId, cancellationToken);

            _db.AuditLogs.Add(new AuditLog
            {
                CompanyId = companyId,
                UserId = adjustedByUserId,
                ActionCode = "Customer.LoyaltyAdjust",
                EntityType = nameof(Customer),
                EntityId = customerId.ToString(),
                OldValueJson = System.Text.Json.JsonSerializer.Serialize(new { PointsBalance = before }),
                NewValueJson = System.Text.Json.JsonSerializer.Serialize(new { PointsBalance = customer.LoyaltyPointsBalance, request.PointsChange, request.Reason }),
                CreatedAtUtc = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken);
        });
    }

    public async Task<IReadOnlyList<MembershipTierDto>> GetTiersAsync(long companyId, CancellationToken cancellationToken = default)
    {
        return await _db.MembershipTiers
            .Where(t => t.CompanyId == companyId && t.IsActive)
            .OrderBy(t => t.MinimumPoints)
            .Select(t => new MembershipTierDto { Id = t.Id, Name = t.Name, MinimumPoints = t.MinimumPoints, PointsMultiplier = t.PointsMultiplier })
            .ToListAsync(cancellationToken);
    }

    public async Task<MembershipTierDto> CreateTierAsync(long companyId, CreateMembershipTierRequest request, CancellationToken cancellationToken = default)
    {
        await _tierValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _db.MembershipTiers.AnyAsync(t => t.CompanyId == companyId && t.Name == request.Name, cancellationToken))
        {
            throw new ConflictException($"A membership tier named '{request.Name}' already exists.");
        }

        var tier = new MembershipTier { CompanyId = companyId, Name = request.Name, MinimumPoints = request.MinimumPoints, PointsMultiplier = request.PointsMultiplier };
        _db.MembershipTiers.Add(tier);
        await _db.SaveChangesAsync(cancellationToken);

        return new MembershipTierDto { Id = tier.Id, Name = tier.Name, MinimumPoints = tier.MinimumPoints, PointsMultiplier = tier.PointsMultiplier };
    }

    private async Task PostLoyaltyTransactionAsync(
        Customer customer,
        LoyaltyTransactionType type,
        int pointsChange,
        string? referenceType,
        long? referenceId,
        string? notes,
        long? userId,
        CancellationToken cancellationToken)
    {
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            CompanyId = customer.CompanyId,
            CustomerId = customer.Id,
            TransactionType = type,
            PointsChange = pointsChange,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            Notes = notes,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        });

        customer.LoyaltyPointsBalance += pointsChange;

        var newTier = await _db.MembershipTiers
            .Where(t => t.CompanyId == customer.CompanyId && t.IsActive && t.MinimumPoints <= customer.LoyaltyPointsBalance)
            .OrderByDescending(t => t.MinimumPoints)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
        customer.MembershipTierId = newTier;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
