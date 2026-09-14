using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UniversalPOS.Application.Cash.Dtos;
using UniversalPOS.Application.Common.Exceptions;
using UniversalPOS.Application.Common.Interfaces;
using UniversalPOS.Domain.Cash;
using UniversalPOS.Domain.Sales;

namespace UniversalPOS.Application.Cash;

public class CashService : ICashService
{
    private readonly IApplicationDbContext _db;
    private readonly IValidator<OpenShiftRequest> _openValidator;
    private readonly IValidator<RecordCashMovementRequest> _movementValidator;
    private readonly IValidator<CloseShiftRequest> _closeValidator;

    public CashService(
        IApplicationDbContext db,
        IValidator<OpenShiftRequest> openValidator,
        IValidator<RecordCashMovementRequest> movementValidator,
        IValidator<CloseShiftRequest> closeValidator)
    {
        _db = db;
        _openValidator = openValidator;
        _movementValidator = movementValidator;
        _closeValidator = closeValidator;
    }

    public async Task<ShiftDto> OpenShiftAsync(long companyId, long branchId, long cashierUserId, OpenShiftRequest request, CancellationToken cancellationToken = default)
    {
        await _openValidator.ValidateAndThrowAsync(request, cancellationToken);

        var terminalBusy = await _db.CashierShifts.AnyAsync(s => s.TerminalId == request.TerminalId && s.Status == ShiftStatus.Open, cancellationToken);
        if (terminalBusy)
        {
            throw new ConflictException("This terminal already has an open shift. Close it before opening a new one.");
        }

        var userBusy = await _db.CashierShifts.AnyAsync(s => s.CashierUserId == cashierUserId && s.Status == ShiftStatus.Open, cancellationToken);
        if (userBusy)
        {
            throw new ConflictException("You already have an open shift on another terminal.");
        }

        var shift = new CashierShift
        {
            CompanyId = companyId,
            BranchId = branchId,
            TerminalId = request.TerminalId,
            CashierUserId = cashierUserId,
            OpeningFloat = request.OpeningFloat,
            Status = ShiftStatus.Open,
            OpenedAtUtc = DateTime.UtcNow,
        };
        _db.CashierShifts.Add(shift);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(shift);
    }

    public async Task<ShiftDto> GetShiftAsync(long companyId, long shiftId, CancellationToken cancellationToken = default)
    {
        var shift = await _db.CashierShifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(CashierShift), shiftId);
        return ToDto(shift);
    }

    public async Task<ShiftDto?> GetOpenShiftAsync(long companyId, long terminalId, long cashierUserId, CancellationToken cancellationToken = default)
    {
        var shift = await _db.CashierShifts.FirstOrDefaultAsync(
            s => s.CompanyId == companyId && s.TerminalId == terminalId && s.CashierUserId == cashierUserId && s.Status == ShiftStatus.Open,
            cancellationToken);
        return shift is null ? null : ToDto(shift);
    }

    public async Task<ShiftDto> RecordMovementAsync(long companyId, long shiftId, long userId, RecordCashMovementRequest request, CancellationToken cancellationToken = default)
    {
        await _movementValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shift = await _db.CashierShifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(CashierShift), shiftId);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new ConflictException("Cannot record a cash movement on a closed shift.");
        }

        _db.CashMovements.Add(new CashMovement
        {
            CashierShiftId = shift.Id,
            MovementType = request.MovementType,
            Amount = request.Amount,
            Reason = request.Reason,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(shift);
    }

    public async Task<ShiftDto> CloseShiftAsync(long companyId, long shiftId, CloseShiftRequest request, CancellationToken cancellationToken = default)
    {
        await _closeValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shift = await _db.CashierShifts.Include(s => s.Movements)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(CashierShift), shiftId);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new ConflictException($"Shift {shiftId} is already closed.");
        }

        var cashSales = await _db.SalePayments
            .Where(p => p.Method == PaymentMethod.Cash)
            .Join(_db.SaleHeaders, p => p.SaleHeaderId, h => h.Id, (p, h) => new { p.Amount, h.CashierShiftId, h.Status })
            .Where(x => x.CashierShiftId == shiftId && x.Status == SaleStatus.Completed)
            .SumAsync(x => x.Amount, cancellationToken);

        var cashIn = shift.Movements.Where(m => m.MovementType == CashMovementType.CashIn).Sum(m => m.Amount);
        var cashOut = shift.Movements.Where(m => m.MovementType is CashMovementType.CashOut or CashMovementType.Petty).Sum(m => m.Amount);

        var expectedCash = shift.OpeningFloat + cashSales + cashIn - cashOut;

        shift.ClosingFloatCounted = request.ClosingFloatCounted;
        shift.ExpectedCash = Domain.Sales.Money.Round(expectedCash);
        shift.VarianceAmount = Domain.Sales.Money.Round(request.ClosingFloatCounted - expectedCash);
        shift.Status = ShiftStatus.Closed;
        shift.ClosedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(shift);
    }

    public async Task<DayEndReportDto> GenerateDayEndReportAsync(long companyId, long branchId, DateOnly businessDate, long userId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.DayEndReports.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.BranchId == branchId && r.BusinessDate == businessDate, cancellationToken);
        if (existing is not null && existing.Status == DayEndReportStatus.Finalized)
        {
            // Finalized reports are locked — never recomputed, protecting them from
            // later tampering (e.g. a backdated void changing what was already closed).
            return ToDto(existing);
        }

        // The "business day" boundary is configurable per branch (BusinessDayCutoffHour,
        // default 0 = plain midnight): a branch trading past midnight can set this so a
        // 1am sale still counts as the previous day's Z-report rather than starting a
        // new one.
        var cutoffHour = await _db.Branches.Where(b => b.Id == branchId).Select(b => b.BusinessDayCutoffHour).FirstOrDefaultAsync(cancellationToken);
        var dayStart = businessDate.ToDateTime(new TimeOnly(cutoffHour, 0), DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var completedSales = await _db.SaleHeaders
            .Where(s => s.CompanyId == companyId && s.BranchId == branchId && s.Status == SaleStatus.Completed
                && s.CompletedAtUtc >= dayStart && s.CompletedAtUtc < dayEnd)
            .ToListAsync(cancellationToken);

        var voidedSales = await _db.SaleHeaders
            .Where(s => s.CompanyId == companyId && s.BranchId == branchId && s.Status == SaleStatus.Voided
                && s.CompletedAtUtc >= dayStart && s.CompletedAtUtc < dayEnd)
            .ToListAsync(cancellationToken);

        // RefundTotal covers both a full void (the sale itself is reversed) and a real
        // Refunded credit-note sale (a distinct document — see SalesService.RefundSaleAsync).
        var refundedSales = await _db.SaleHeaders
            .Where(s => s.CompanyId == companyId && s.BranchId == branchId && s.Status == SaleStatus.Refunded
                && s.CompletedAtUtc >= dayStart && s.CompletedAtUtc < dayEnd)
            .ToListAsync(cancellationToken);

        var saleIds = completedSales.Select(s => s.Id).ToList();
        var payments = await _db.SalePayments.Where(p => saleIds.Contains(p.SaleHeaderId)).ToListAsync(cancellationToken);

        var report = existing ?? new DayEndReport { CompanyId = companyId, BranchId = branchId, BusinessDate = businessDate, CreatedAtUtc = DateTime.UtcNow };

        report.GrossSales = completedSales.Sum(s => s.SubTotal);
        report.DiscountTotal = completedSales.Sum(s => s.DiscountTotal);
        report.TaxTotal = completedSales.Sum(s => s.TaxTotal);
        report.ServiceChargeTotal = completedSales.Sum(s => s.ServiceChargeTotal);
        report.NetSales = completedSales.Sum(s => s.GrandTotal);
        report.RefundTotal = voidedSales.Sum(s => s.GrandTotal) + refundedSales.Sum(s => s.GrandTotal);
        report.CashSalesTotal = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
        report.CardSalesTotal = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount);
        report.OtherPaymentTotal = payments.Where(p => p.Method is not PaymentMethod.Cash and not PaymentMethod.Card).Sum(p => p.Amount);
        report.TransactionCount = completedSales.Count;
        report.VoidCount = voidedSales.Count;
        report.GeneratedByUserId = userId;

        if (existing is null)
        {
            _db.DayEndReports.Add(report);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(report);
    }

    public async Task<DayEndReportDto> FinalizeDayEndReportAsync(long companyId, long reportId, CancellationToken cancellationToken = default)
    {
        var report = await _db.DayEndReports.FirstOrDefaultAsync(r => r.Id == reportId && r.CompanyId == companyId, cancellationToken)
            ?? throw new NotFoundException(nameof(DayEndReport), reportId);

        if (report.Status == DayEndReportStatus.Finalized)
        {
            throw new ConflictException($"Day-end report {reportId} is already finalized.");
        }

        report.Status = DayEndReportStatus.Finalized;
        report.FinalizedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(report);
    }

    public async Task<IReadOnlyList<DayEndReportDto>> GetDayEndReportHistoryAsync(long companyId, long branchId, CancellationToken cancellationToken = default)
    {
        return await _db.DayEndReports
            .Where(r => r.CompanyId == companyId && r.BranchId == branchId)
            .OrderByDescending(r => r.BusinessDate)
            .Select(r => ToDto(r))
            .ToListAsync(cancellationToken);
    }

    public async Task OpenCashDrawerAsync(long companyId, long branchId, long? terminalId, long userId, CancellationToken cancellationToken = default)
    {
        // No physical drawer exists in this environment (that's Phase 12's hardware
        // abstraction) — but the permission-gated action and its audit trail are real:
        // a manager opening the drawer outside of a sale is exactly the kind of event
        // a business wants a durable record of, hardware or not.
        _db.AuditLogs.Add(new Domain.Auditing.AuditLog
        {
            CompanyId = companyId,
            BranchId = branchId,
            TerminalId = terminalId,
            UserId = userId,
            ActionCode = "Cash.DrawerOpen",
            EntityType = "Terminal",
            EntityId = terminalId?.ToString() ?? "unknown",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ShiftDto ToDto(CashierShift s) => new()
    {
        Id = s.Id,
        TerminalId = s.TerminalId,
        CashierUserId = s.CashierUserId,
        OpeningFloat = s.OpeningFloat,
        ClosingFloatCounted = s.ClosingFloatCounted,
        ExpectedCash = s.ExpectedCash,
        VarianceAmount = s.VarianceAmount,
        Status = s.Status.ToString(),
        OpenedAtUtc = s.OpenedAtUtc,
        ClosedAtUtc = s.ClosedAtUtc,
    };

    private static DayEndReportDto ToDto(DayEndReport r) => new()
    {
        Id = r.Id,
        BusinessDate = r.BusinessDate,
        GrossSales = r.GrossSales,
        DiscountTotal = r.DiscountTotal,
        TaxTotal = r.TaxTotal,
        ServiceChargeTotal = r.ServiceChargeTotal,
        NetSales = r.NetSales,
        RefundTotal = r.RefundTotal,
        CashSalesTotal = r.CashSalesTotal,
        CardSalesTotal = r.CardSalesTotal,
        OtherPaymentTotal = r.OtherPaymentTotal,
        TransactionCount = r.TransactionCount,
        VoidCount = r.VoidCount,
        Status = r.Status.ToString(),
    };
}
