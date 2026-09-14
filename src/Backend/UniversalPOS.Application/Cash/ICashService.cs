using UniversalPOS.Application.Cash.Dtos;

namespace UniversalPOS.Application.Cash;

public interface ICashService
{
    Task<ShiftDto> OpenShiftAsync(long companyId, long branchId, long cashierUserId, OpenShiftRequest request, CancellationToken cancellationToken = default);
    Task<ShiftDto> GetShiftAsync(long companyId, long shiftId, CancellationToken cancellationToken = default);
    Task<ShiftDto?> GetOpenShiftAsync(long companyId, long terminalId, long cashierUserId, CancellationToken cancellationToken = default);
    Task<ShiftDto> RecordMovementAsync(long companyId, long shiftId, long userId, RecordCashMovementRequest request, CancellationToken cancellationToken = default);
    Task<ShiftDto> CloseShiftAsync(long companyId, long shiftId, CloseShiftRequest request, CancellationToken cancellationToken = default);

    Task<DayEndReportDto> GenerateDayEndReportAsync(long companyId, long branchId, DateOnly businessDate, long userId, CancellationToken cancellationToken = default);
    Task<DayEndReportDto> FinalizeDayEndReportAsync(long companyId, long reportId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DayEndReportDto>> GetDayEndReportHistoryAsync(long companyId, long branchId, CancellationToken cancellationToken = default);

    Task OpenCashDrawerAsync(long companyId, long branchId, long? terminalId, long userId, CancellationToken cancellationToken = default);
}
