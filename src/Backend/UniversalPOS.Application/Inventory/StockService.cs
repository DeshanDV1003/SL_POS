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
    private readonly IValidator<CreateStockTransferRequest> _transferValidator;
    private readonly IValidator<SubmitStockCountLineRequest> _countLineValidator;

    public StockService(
        IApplicationDbContext db,
        IValidator<CreateStockAdjustmentRequest> adjustmentValidator,
        IValidator<CreateStockTransferRequest> transferValidator,
        IValidator<SubmitStockCountLineRequest> countLineValidator)
    {
        _db = db;
        _adjustmentValidator = adjustmentValidator;
        _transferValidator = transferValidator;
        _countLineValidator = countLineValidator;
    }

    public async Task<decimal> PostMovementAsync(
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
            return quantityChange;
        }

        summary.QuantityOnHand += quantityChange;
        return summary.QuantityOnHand;
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

    public async Task<StockTransferDto> CreateTransferAsync(long companyId, long fromBranchId, long requestedByUserId, CreateStockTransferRequest request, CancellationToken cancellationToken = default)
    {
        await _transferValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (request.ToBranchId == fromBranchId)
        {
            throw new ConflictException("Cannot transfer stock from a branch to itself.");
        }

        if (!await _db.Branches.AnyAsync(b => b.Id == request.ToBranchId && b.CompanyId == companyId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.ToBranchId);
        }

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        if (await _db.Products.CountAsync(p => p.CompanyId == companyId && productIds.Contains(p.Id), cancellationToken) != productIds.Count)
        {
            throw new NotFoundException("Product", string.Join(",", productIds));
        }

        var transfer = new StockTransfer
        {
            CompanyId = companyId,
            FromBranchId = fromBranchId,
            ToBranchId = request.ToBranchId,
            RequestedByUserId = requestedByUserId,
            Status = StockTransferStatus.Requested,
            CreatedAtUtc = DateTime.UtcNow,
        };
        foreach (var line in request.Lines)
        {
            transfer.Lines.Add(new StockTransferLine { ProductId = line.ProductId, Quantity = line.Quantity });
        }

        _db.StockTransfers.Add(transfer);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(transfer);
    }

    public async Task<StockTransferDto> SendTransferAsync(long companyId, long transferId, long sentByUserId, CancellationToken cancellationToken = default)
    {
        var transfer = await _db.StockTransfers.Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == transferId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(StockTransfer), transferId);

        if (transfer.Status != StockTransferStatus.Requested)
        {
            throw new ConflictException($"Transfer {transferId} is not awaiting dispatch ({transfer.Status}).");
        }

        await _db.ExecuteInTransactionAsync(async () =>
        {
            foreach (var line in transfer.Lines)
            {
                await PostMovementAsync(companyId, transfer.FromBranchId, line.ProductId, StockMovementType.TransferOut, -line.Quantity,
                    nameof(StockTransfer), transfer.Id, sentByUserId, cancellationToken);
            }

            transfer.Status = StockTransferStatus.Sent;
            transfer.SentByUserId = sentByUserId;
            transfer.SentAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        });

        return ToDto(transfer);
    }

    public async Task<StockTransferDto> ReceiveTransferAsync(long companyId, long transferId, long receivedByUserId, CancellationToken cancellationToken = default)
    {
        var transfer = await _db.StockTransfers.Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == transferId && t.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(StockTransfer), transferId);

        if (transfer.Status != StockTransferStatus.Sent)
        {
            throw new ConflictException($"Transfer {transferId} has not been sent yet ({transfer.Status}).");
        }

        await _db.ExecuteInTransactionAsync(async () =>
        {
            foreach (var line in transfer.Lines)
            {
                await PostMovementAsync(companyId, transfer.ToBranchId, line.ProductId, StockMovementType.TransferIn, line.Quantity,
                    nameof(StockTransfer), transfer.Id, receivedByUserId, cancellationToken);
            }

            transfer.Status = StockTransferStatus.Received;
            transfer.ReceivedByUserId = receivedByUserId;
            transfer.ReceivedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        });

        return ToDto(transfer);
    }

    public async Task<IReadOnlyList<StockTransferDto>> GetTransfersAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        var transfers = await _db.StockTransfers.Include(t => t.Lines)
            .Where(t => t.CompanyId == companyId && (t.FromBranchId == branchId || t.ToBranchId == branchId))
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return transfers.Select(ToDto).ToList();
    }

    private static StockTransferDto ToDto(StockTransfer t) => new()
    {
        Id = t.Id,
        FromBranchId = t.FromBranchId,
        ToBranchId = t.ToBranchId,
        Status = t.Status.ToString(),
        CreatedAtUtc = t.CreatedAtUtc,
        Lines = t.Lines.Select(l => new CreateStockTransferLineRequest { ProductId = l.ProductId, Quantity = l.Quantity }).ToList(),
    };

    public async Task<StockCountDto> CreateCountAsync(long companyId, long branchId, long createdByUserId, CreateStockCountRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.StockOnHands.Where(s => s.CompanyId == companyId && s.BranchId == branchId);
        if (request.ProductIds.Count > 0)
        {
            query = query.Where(s => request.ProductIds.Contains(s.ProductId));
        }

        var stockRows = await query.ToListAsync(cancellationToken);

        var count = new StockCount
        {
            CompanyId = companyId,
            BranchId = branchId,
            CreatedByUserId = createdByUserId,
            Status = StockCountStatus.InProgress,
            CreatedAtUtc = DateTime.UtcNow,
        };
        foreach (var row in stockRows)
        {
            count.Lines.Add(new StockCountLine { ProductId = row.ProductId, SystemQuantity = row.QuantityOnHand });
        }

        _db.StockCounts.Add(count);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildStockCountDtoAsync(count, cancellationToken);
    }

    public async Task<StockCountDto> SubmitCountLinesAsync(long companyId, long stockCountId, List<SubmitStockCountLineRequest> lines, CancellationToken cancellationToken = default)
    {
        foreach (var line in lines)
        {
            await _countLineValidator.ValidateAndThrowAsync(line, cancellationToken);
        }

        var count = await _db.StockCounts.Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == stockCountId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(StockCount), stockCountId);

        if (count.Status != StockCountStatus.InProgress)
        {
            throw new ConflictException($"Stock count {stockCountId} is not in progress ({count.Status}).");
        }

        foreach (var lineRequest in lines)
        {
            var line = count.Lines.FirstOrDefault(l => l.ProductId == lineRequest.ProductId)
                ?? throw new NotFoundException("StockCountLine for product", lineRequest.ProductId);
            line.CountedQuantity = lineRequest.CountedQuantity;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await BuildStockCountDtoAsync(count, cancellationToken);
    }

    public async Task<StockCountDto> CompleteCountAsync(long companyId, long stockCountId, long completedByUserId, CancellationToken cancellationToken = default)
    {
        var count = await _db.StockCounts.Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == stockCountId && c.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(StockCount), stockCountId);

        if (count.Status != StockCountStatus.InProgress)
        {
            throw new ConflictException($"Stock count {stockCountId} is not in progress ({count.Status}).");
        }

        if (count.Lines.Any(l => !l.CountedQuantity.HasValue))
        {
            throw new ConflictException("Every line must have a counted quantity before the count can be completed.");
        }

        await _db.ExecuteInTransactionAsync(async () =>
        {
            foreach (var line in count.Lines)
            {
                var delta = line.CountedQuantity!.Value - line.SystemQuantity;
                if (delta != 0)
                {
                    await PostMovementAsync(companyId, count.BranchId, line.ProductId, StockMovementType.StockCount, delta,
                        nameof(StockCount), count.Id, completedByUserId, cancellationToken);
                }
            }

            count.Status = StockCountStatus.Completed;
            count.CompletedByUserId = completedByUserId;
            count.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        });

        return await BuildStockCountDtoAsync(count, cancellationToken);
    }

    private async Task<StockCountDto> BuildStockCountDtoAsync(StockCount count, CancellationToken cancellationToken)
    {
        var productIds = count.Lines.Select(l => l.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        return new StockCountDto
        {
            Id = count.Id,
            Status = count.Status.ToString(),
            CreatedAtUtc = count.CreatedAtUtc,
            Lines = count.Lines.Select(l => new StockCountLineDto
            {
                ProductId = l.ProductId,
                ProductName = products.TryGetValue(l.ProductId, out var p) ? p.Name : "(unknown)",
                SystemQuantity = l.SystemQuantity,
                CountedQuantity = l.CountedQuantity,
            }).ToList(),
        };
    }

    public async Task<IReadOnlyList<StockReconciliationFlagDto>> GetReconciliationFlagsAsync(long companyId, long branchId, bool openOnly, CancellationToken cancellationToken = default)
    {
        var query = _db.StockReconciliationFlags.Where(f => f.CompanyId == companyId && f.BranchId == branchId);
        if (openOnly)
        {
            query = query.Where(f => f.Status == StockReconciliationFlagStatus.Open);
        }

        var flags = await query.OrderByDescending(f => f.CreatedAtUtc).ToListAsync(cancellationToken);
        return await BuildFlagDtosAsync(flags, cancellationToken);
    }

    public async Task<StockReconciliationFlagDto> ResolveReconciliationFlagAsync(long companyId, long flagId, long resolvedByUserId, ResolveStockReconciliationFlagRequest request, CancellationToken cancellationToken = default)
    {
        var flag = await _db.StockReconciliationFlags.FirstOrDefaultAsync(f => f.Id == flagId && f.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(StockReconciliationFlag), flagId);

        if (flag.Status == StockReconciliationFlagStatus.Resolved)
        {
            throw new ConflictException("This stock-reconciliation flag has already been resolved.");
        }

        flag.Status = StockReconciliationFlagStatus.Resolved;
        flag.ResolvedByUserId = resolvedByUserId;
        flag.ResolvedAtUtc = DateTime.UtcNow;
        flag.ResolutionNotes = request.ResolutionNotes;
        await _db.SaveChangesAsync(cancellationToken);

        return (await BuildFlagDtosAsync(new List<StockReconciliationFlag> { flag }, cancellationToken)).Single();
    }

    private async Task<IReadOnlyList<StockReconciliationFlagDto>> BuildFlagDtosAsync(List<StockReconciliationFlag> flags, CancellationToken cancellationToken)
    {
        var productIds = flags.Select(f => f.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var saleIds = flags.Select(f => f.SaleHeaderId).Distinct().ToList();
        var invoiceNumbers = await _db.SaleHeaders.Where(s => saleIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.InvoiceNumber, cancellationToken);

        return flags.Select(f => new StockReconciliationFlagDto
        {
            Id = f.Id,
            ProductId = f.ProductId,
            ProductName = products.TryGetValue(f.ProductId, out var p) ? p.Name : "(unknown)",
            SaleHeaderId = f.SaleHeaderId,
            SaleInvoiceNumber = invoiceNumbers.GetValueOrDefault(f.SaleHeaderId),
            ShortfallQuantity = f.ShortfallQuantity,
            Status = f.Status.ToString(),
            CreatedAtUtc = f.CreatedAtUtc,
            ResolvedAtUtc = f.ResolvedAtUtc,
            ResolutionNotes = f.ResolutionNotes,
        }).ToList();
    }
}
