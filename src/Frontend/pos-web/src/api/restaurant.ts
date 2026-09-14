import { apiFetch, apiFetchText } from './client';
import type { SalePaymentRequest } from './sales';

export interface TableInfo {
  id: number;
  floorId: number;
  name: string;
  capacity: number;
  status: 'Available' | 'Occupied' | 'Reserved' | 'Cleaning' | 'Billing';
  openOrderId: number | null;
}

export interface FloorInfo {
  id: number;
  name: string;
  tables: TableInfo[];
}

export interface OrderLine {
  id: number;
  productId: number;
  productName: string;
  quantity: number;
  notes: string | null;
  kotStatus: string;
}

export interface OrderInfo {
  id: number;
  tableId: number | null;
  orderType: string;
  status: string;
  createdAtUtc: string;
  lines: OrderLine[];
}

export interface KitchenStationInfo {
  id: number;
  name: string;
  category: string;
}

export interface TicketLine {
  productId: number;
  productName: string;
  quantity: number;
  notes: string | null;
}

export interface TicketInfo {
  id: number;
  ticketNumber: string;
  orderId: number;
  tableId: number | null;
  status: string;
  createdAtUtc: string;
  lines: TicketLine[];
}

export function getFloors(branchId: number): Promise<FloorInfo[]> {
  return apiFetch<FloorInfo[]>(`/api/v1/branches/${branchId}/floors`);
}

export function openTable(branchId: number, tableId: number): Promise<OrderInfo> {
  return apiFetch<OrderInfo>(`/api/v1/branches/${branchId}/tables/${tableId}/open`, {
    method: 'POST',
    body: JSON.stringify({ orderType: 'DineIn' }),
  });
}

export function getOrder(branchId: number, orderId: number): Promise<OrderInfo> {
  return apiFetch<OrderInfo>(`/api/v1/branches/${branchId}/orders/${orderId}`);
}

export function addOrderLines(branchId: number, orderId: number, lines: { productId: number; quantity: number }[]): Promise<OrderInfo> {
  return apiFetch<OrderInfo>(`/api/v1/branches/${branchId}/orders/${orderId}/lines`, {
    method: 'POST',
    body: JSON.stringify(lines),
  });
}

export function sendToKitchen(branchId: number, orderId: number): Promise<TicketInfo[]> {
  return apiFetch<TicketInfo[]>(`/api/v1/branches/${branchId}/orders/${orderId}/send-to-kitchen`, { method: 'POST' });
}

export function billOrder(branchId: number, orderId: number, terminalId: number, payments: SalePaymentRequest[]) {
  return apiFetch(`/api/v1/branches/${branchId}/orders/${orderId}/bill`, {
    method: 'POST',
    body: JSON.stringify({ terminalId, payments }),
  });
}

export function getKitchenStations(branchId: number): Promise<KitchenStationInfo[]> {
  return apiFetch<KitchenStationInfo[]>(`/api/v1/branches/${branchId}/kitchen-stations`);
}

export function getKdsTickets(branchId: number, stationId: number): Promise<TicketInfo[]> {
  return apiFetch<TicketInfo[]>(`/api/v1/branches/${branchId}/kitchen-stations/${stationId}/tickets`);
}

export function updateTicketStatus(branchId: number, ticketId: number, status: string): Promise<TicketInfo> {
  return apiFetch<TicketInfo>(`/api/v1/branches/${branchId}/tickets/${ticketId}/status`, {
    method: 'POST',
    body: JSON.stringify({ status }),
  });
}

/** The formatted plain-text payload a kitchen/bar print adapter would send verbatim — see docs/architecture.md §11. */
export function getTicketPrintPayload(branchId: number, ticketId: number): Promise<string> {
  return apiFetchText(`/api/v1/branches/${branchId}/tickets/${ticketId}/print`);
}
