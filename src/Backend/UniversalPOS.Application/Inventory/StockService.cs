using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Application.Inventory.Dtos;
using UniversalPOS.Domain.Inventory;

namespace UniversalPOS.Application.Inventory;

public class StockService : IStockService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<CreateStockAdjustmentRequest> _adjustmentValidator;

    public StockService(IApplicationDbContext db, IValidator<CreateStockAdjustmentRequest> adjustmentValidator)
    {
        _db = db;
        _adjustmentValidator = adjustmentValidator;
    }

    public async Task PostMovementAsync(
        long companyId,
        long branchId,
        long productId,
        StockMovementType movementType,
        decimal quantityChange,
        string referenceType,
        long referenceId,
        long? userId,
        CancellationToken cancellationToken = default)
    {
        _db.StockLedgers.Add(new StockLedger
        {
            CompanyId = companyId,
            BranchId = branchId,
            ProductId = productId,
            MovementType = movementType,
            QuantityChange = quantityChange,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        });

        var summary = await _db.StockOnHands
            .FirstOrDefaultAsync(s => s.BranchId == branchId && s.ProductId == productId, cancellationToken);

        if (summary is null)
        {
            // Negative stock is intentionally not blocked here — see docs/architecture.md
            // §10 (offline sync can post a sale before its stock check reconciles) and
            // §12 of the master brief ("negative-stock rules" are a future per-Company
            // config point, not a hardcoded constraint).
            _db.StockOnHands.Add(new StockOnHand
            {
                CompanyId = companyId,
                BranchId = branchId,
                ProductId = productId,
                QuantityOnHand = quantityChange,
            });
        }
        else
        {
            summary.QuantityOnHand += quantityChange;
        }
    }

    public async Task<IReadOnlyList<StockOnHandDto>> GetStockOnHandAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        return await (
            from soh in _db.StockOnHands
            join p in _db.Products on soh.ProductId equals p.Id
            where soh.CompanyId == companyId && soh.BranchId == branchId
            orderby p.Name
            select new StockOnHandDto
            {
                ProductId = p.Id,
                ProductName = p.Name,
                Sku = p.Sku,
                QuantityOnHand = soh.QuantityOnHand,
                ReorderLevel = p.ReorderLevel,
                IsBelowReorderLevel = soh.QuantityOnHand <= p.ReorderLevel,
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockLedgerEntryDto>> GetLedgerAsync(long companyId, long branchId, long productId, CancellationToken cancellationToken = default)
    {
        return await _db.StockLedgers
            .Where(l => l.CompanyId == companyId && l.BranchId == branchId && l.ProductId == productId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new StockLedgerEntryDto
            {
                Id = l.Id,
                ProductId = l.ProductId,
                MovementType = l.MovementType.ToString(),
                QuantityChange = l.QuantityChange,
                ReferenceType = l.ReferenceType,
                ReferenceId = l.ReferenceId,
                CreatedAtUtc = l.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StockAdjustmentDto> CreateAdjustmentAsync(long companyId, long branchId, long requestedByUserId, CreateStockAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        await _adjustmentValidator.ValidateAndThrowAsync(request, cancellationToken);

        var productIds = request.Lines.Select(l => l.ProductId).ToList();
        var validProductCount = await _db.Products.CountAsync(p => p.CompanyId == companyId && productIds.Contains(p.Id), cancellationToken);
        if (validProductCount != productIds.Distinct().Count())
        {
            throw new NotFoundException("Product", string.Join(",", productIds));
        }

        var adjustment = new StockAdjustment
        {
            CompanyId = companyId,
            BranchId = branchId,
            Reason = request.Reason,
            Notes = request.Notes,
            RequestedByUserId = requestedByUserId,
            Status = StockAdjustmentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
        };

        foreach (var line in request.Lines)
        {
            adjustment.Lines.Add(new StockAdjustmentLine { ProductId = line.ProductId, QuantityChange = line.QuantityChange });
        }

        _db.StockAdjustments.Add(adjustment);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(adjustment);
    }

    public async Task<StockAdjustmentDto> ApproveAdjustmentAsync(long companyId, long adjustmentId, long approvedByUserId, CancellationToken cancellationToken = default)
    {
        var adjustment = await _db.StockAdjustments
            .Include(a => a.Lines)
            .FirstOrDefaultAsync(a => a.Id == adjustmentId && a.CompanyId == companyId, cancellationToken);

        if (adjustment is null)
        {
            throw new NotFoundException(nameof(StockAdjustment), adjustmentId);
        }

        if (adjustment.Status != StockAdjustmentStatus.Pending)
        {
            throw new ConflictException($"Stock adjustment {adjustmentId} has already been resolved.");
        }

        if (adjustment.RequestedByUserId == approvedByUserId)
        {
            throw new ForbiddenException("A stock adjustment cannot be approved by the same user who requested it.");
        }

        foreach (var line in adjustment.Lines)
        {
            await PostMovementAsync(
                companyId,
                adjustment.BranchId,
                line.ProductId,
                line.QuantityChange >= 0 ? StockMovementType.AdjustmentIncrease : StockMovementType.AdjustmentDecrease,
                line.QuantityChange,
                nameof(StockAdjustment),
                adjustment.Id,
                approvedByUserId,
                cancellationToken);
        }

        adjustment.Status = StockAdjustmentStatus.Approved;
        adjustment.ApprovedByUserId = approvedByUserId;
        adjustment.ResolvedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(adjustment);
    }

    public async Task<IReadOnlyList<StockAdjustmentDto>> GetPendingAdjustmentsAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        var adjustments = await _db.StockAdjustments
            .Include(a => a.Lines)
            .Where(a => a.CompanyId == companyId && a.BranchId == branchId && a.Status == StockAdjustmentStatus.Pending)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return adjustments.Select(ToDto).ToList();
    }

    private static StockAdjustmentDto ToDto(StockAdjustment a) => new()
    {
        Id = a.Id,
        Reason = a.Reason.ToString(),
        Status = a.Status.ToString(),
        Notes = a.Notes,
        CreatedAtUtc = a.CreatedAtUtc,
        Lines = a.Lines.Select(l => new CreateStockAdjustmentLineRequest { ProductId = l.ProductId, QuantityChange = l.QuantityChange }).ToList(),
    };
}
