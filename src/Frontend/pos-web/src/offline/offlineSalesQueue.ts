import type { CreateSaleRequest } from '../api/sales';
import { getAllFromStore, withStore } from './db';

export type QueuedSaleStatus = 'queued' | 'syncing' | 'failed';

export interface QueuedSale {
  /** Also CreateSaleRequest.clientIdempotencyKey — the IndexedDB store's key path. */
  clientIdempotencyKey: string;
  branchId: number;
  request: CreateSaleRequest;
  /** When it was queued on this device — same as request.clientCreatedAtUtc, kept alongside for sorting without re-parsing the request. */
  createdAtUtc: string;
  attempts: number;
  status: QueuedSaleStatus;
  lastError: string | null;
}

export function generateIdempotencyKey(): string {
  if ('randomUUID' in crypto) return crypto.randomUUID();
  // Fallback for a non-secure-context dev server without crypto.randomUUID.
  return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

export async function enqueueOfflineSale(branchId: number, request: Omit<CreateSaleRequest, 'isOfflineSync' | 'clientIdempotencyKey' | 'clientCreatedAtUtc'>): Promise<QueuedSale> {
  const clientIdempotencyKey = generateIdempotencyKey();
  const createdAtUtc = new Date().toISOString();

  const queued: QueuedSale = {
    clientIdempotencyKey,
    branchId,
    request: { ...request, isOfflineSync: true, clientIdempotencyKey, clientCreatedAtUtc: createdAtUtc },
    createdAtUtc,
    attempts: 0,
    status: 'queued',
    lastError: null,
  };

  await withStore('readwrite', (store) => store.add(queued));
  return queued;
}

export function getQueuedSales(): Promise<QueuedSale[]> {
  return getAllFromStore<QueuedSale>();
}

export function removeQueuedSale(clientIdempotencyKey: string): Promise<void> {
  return withStore('readwrite', (store) => store.delete(clientIdempotencyKey)) as unknown as Promise<void>;
}

export async function updateQueuedSale(sale: QueuedSale): Promise<void> {
  await withStore('readwrite', (store) => store.put(sale));
}
