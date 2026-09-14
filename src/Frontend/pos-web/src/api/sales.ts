import { apiFetch, apiFetchText } from './client';

export interface SaleLineRequest {
  productId: number;
  quantity: number;
  discountPercentage: number;
  unitPriceOverride?: number;
}

export interface SalePaymentRequest {
  method: 'Cash' | 'Card' | 'BankTransfer' | 'Digital' | 'Credit';
  amount: number;
  instrumentToken?: string;
}

export interface CreateSaleRequest {
  terminalId: number;
  customerId?: number;
  lines: SaleLineRequest[];
  payments: SalePaymentRequest[];
  /** True when this sale is being replayed from the offline queue rather than taken live — see docs/architecture.md §10. Requires clientIdempotencyKey. */
  isOfflineSync?: boolean;
  /** Required when isOfflineSync is true, so a retried sync of the same queued sale never double-books it. */
  clientIdempotencyKey?: string;
  /** When the sale actually happened on the terminal — only meaningful when isOfflineSync is true. */
  clientCreatedAtUtc?: string;
}

export interface SaleLineDto {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountPercentage: number;
  lineDiscountAmount: number;
  lineTaxAmount: number;
  lineTotal: number;
}

export interface SaleReceipt {
  id: number;
  invoiceNumber: string | null;
  status: string;
  subTotal: number;
  discountTotal: number;
  taxTotal: number;
  serviceChargeTotal: number;
  grandTotal: number;
  changeDue: number;
  isOfflineSync: boolean;
  completedAtUtc: string;
  lines: SaleLineDto[];
  payments: { method: string; amount: number; providerReference: string | null }[];
}

export function checkout(branchId: number, request: CreateSaleRequest): Promise<SaleReceipt> {
  return apiFetch<SaleReceipt>(`/api/v1/branches/${branchId}/sales`, {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export interface HeldBillDto {
  id: number;
  notes: string | null;
  createdAtUtc: string;
  lines: SaleLineRequest[];
}

export function holdBill(branchId: number, terminalId: number, notes: string | undefined, lines: SaleLineRequest[]): Promise<HeldBillDto> {
  return apiFetch<HeldBillDto>(`/api/v1/branches/${branchId}/sales/held`, {
    method: 'POST',
    body: JSON.stringify({ terminalId, notes, lines }),
  });
}

export function getHeldBills(branchId: number): Promise<HeldBillDto[]> {
  return apiFetch<HeldBillDto[]>(`/api/v1/branches/${branchId}/sales/held`);
}

export function recallBill(branchId: number, heldBillId: number): Promise<HeldBillDto> {
  return apiFetch<HeldBillDto>(`/api/v1/branches/${branchId}/sales/held/${heldBillId}/recall`, { method: 'POST' });
}

export function deleteHeldBill(branchId: number, heldBillId: number): Promise<void> {
  return apiFetch<void>(`/api/v1/branches/${branchId}/sales/held/${heldBillId}`, { method: 'DELETE' });
}

export function getReceiptText(branchId: number, saleId: number): Promise<string> {
  return apiFetchText(`/api/v1/branches/${branchId}/sales/${saleId}/receipt`);
}
