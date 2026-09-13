import { apiFetch } from './client';

export interface ShiftDto {
  id: number;
  terminalId: number;
  cashierUserId: number;
  openingFloat: number;
  closingFloatCounted: number | null;
  expectedCash: number | null;
  varianceAmount: number | null;
  status: 'Open' | 'Closed';
  openedAtUtc: string;
  closedAtUtc: string | null;
}

export interface DayEndReportDto {
  id: number;
  businessDate: string;
  grossSales: number;
  discountTotal: number;
  taxTotal: number;
  serviceChargeTotal: number;
  netSales: number;
  refundTotal: number;
  cashSalesTotal: number;
  cardSalesTotal: number;
  otherPaymentTotal: number;
  transactionCount: number;
  voidCount: number;
  status: 'Draft' | 'Finalized';
}

export function getOpenShift(branchId: number, terminalId: number): Promise<ShiftDto | null> {
  return apiFetch<ShiftDto>(`/api/v1/branches/${branchId}/shifts/open?terminalId=${terminalId}`).catch(() => null);
}

export function openShift(branchId: number, terminalId: number, openingFloat: number): Promise<ShiftDto> {
  return apiFetch<ShiftDto>(`/api/v1/branches/${branchId}/shifts`, {
    method: 'POST',
    body: JSON.stringify({ terminalId, openingFloat }),
  });
}

export function recordMovement(branchId: number, shiftId: number, movementType: string, amount: number, reason?: string) {
  return apiFetch(`/api/v1/branches/${branchId}/shifts/${shiftId}/movements`, {
    method: 'POST',
    body: JSON.stringify({ movementType, amount, reason }),
  });
}

export function closeShift(branchId: number, shiftId: number, closingFloatCounted: number): Promise<ShiftDto> {
  return apiFetch<ShiftDto>(`/api/v1/branches/${branchId}/shifts/${shiftId}/close`, {
    method: 'POST',
    body: JSON.stringify({ closingFloatCounted }),
  });
}

export function getDayEndReport(branchId: number, businessDate: string): Promise<DayEndReportDto> {
  return apiFetch<DayEndReportDto>(`/api/v1/branches/${branchId}/day-end-report?businessDate=${businessDate}`);
}

export function finalizeDayEndReport(branchId: number, reportId: number): Promise<DayEndReportDto> {
  return apiFetch<DayEndReportDto>(`/api/v1/branches/${branchId}/day-end-report/${reportId}/finalize`, { method: 'POST' });
}
