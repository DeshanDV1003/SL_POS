import { ApiError } from '../api/client';
import { checkout } from '../api/sales';
import { getQueuedSales, removeQueuedSale, updateQueuedSale, type QueuedSale } from './offlineSalesQueue';

export interface SyncState {
  isOnline: boolean;
  isSyncing: boolean;
  pendingCount: number;
  failedCount: number;
  lastSyncError: string | null;
}

type Listener = (state: SyncState) => void;

const RETRY_INTERVAL_MS = 30_000;

/**
 * Background retry for the offline sales queue — see docs/architecture.md §10.
 * Processes queued sales strictly in the order they were created (oldest first) so
 * the server's sequential invoice numbering assigns numbers in the same order the
 * sales actually happened, not the order network conditions happened to allow them
 * through. A network failure halts the whole pass (still offline, try again later);
 * a real server rejection (e.g. the branch/product no longer exists) marks just
 * that one sale 'failed' — visible for manual review — and moves on to the rest,
 * since a rejected sale never consumed an invoice number and so leaves no gap.
 */
class OfflineSyncManager {
  private listeners = new Set<Listener>();
  private state: SyncState = { isOnline: navigator.onLine, isSyncing: false, pendingCount: 0, failedCount: 0, lastSyncError: null };
  private started = false;
  private intervalHandle: ReturnType<typeof setInterval> | null = null;

  start() {
    if (this.started) return;
    this.started = true;

    window.addEventListener('online', this.handleOnline);
    window.addEventListener('offline', this.handleOffline);
    this.intervalHandle = setInterval(() => void this.sync(), RETRY_INTERVAL_MS);

    void this.refreshPendingCount();
    if (navigator.onLine) void this.sync();
  }

  stop() {
    if (!this.started) return;
    this.started = false;
    window.removeEventListener('online', this.handleOnline);
    window.removeEventListener('offline', this.handleOffline);
    if (this.intervalHandle) clearInterval(this.intervalHandle);
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    listener(this.state);
    return () => this.listeners.delete(listener);
  }

  /** Called by the checkout page right after queueing a new sale, so the badge updates immediately instead of waiting for the next timer tick. */
  async notifyEnqueued() {
    await this.refreshPendingCount();
    if (navigator.onLine) void this.sync();
  }

  async triggerSync(): Promise<void> {
    await this.sync();
  }

  private handleOnline = () => {
    this.setState({ isOnline: true });
    void this.sync();
  };

  private handleOffline = () => {
    this.setState({ isOnline: false });
  };

  private async refreshPendingCount() {
    const all = await getQueuedSales();
    this.setState({
      pendingCount: all.filter((s) => s.status !== 'failed').length,
      failedCount: all.filter((s) => s.status === 'failed').length,
    });
  }

  private async sync() {
    if (this.state.isSyncing || !navigator.onLine) return;

    const queued = (await getQueuedSales()).filter((s) => s.status !== 'failed');
    if (queued.length === 0) {
      await this.refreshPendingCount();
      return;
    }

    this.setState({ isSyncing: true, lastSyncError: null });
    queued.sort((a, b) => a.createdAtUtc.localeCompare(b.createdAtUtc));

    for (const sale of queued) {
      const outcome = await this.syncOne(sale);
      if (outcome === 'network-failure') {
        break; // still offline (or the connection dropped mid-pass) — stop and retry the whole pass later.
      }
    }

    await this.refreshPendingCount();
    this.setState({ isSyncing: false });
  }

  private async syncOne(sale: QueuedSale): Promise<'synced' | 'rejected' | 'network-failure'> {
    try {
      await checkout(sale.branchId, sale.request);
      await removeQueuedSale(sale.clientIdempotencyKey);
      return 'synced';
    } catch (err) {
      if (err instanceof ApiError) {
        // A real answer from the server: this specific sale is rejected (not a
        // connectivity problem), so record it and move on rather than blocking
        // every other queued sale behind it forever.
        await updateQueuedSale({ ...sale, status: 'failed', attempts: sale.attempts + 1, lastError: err.message });
        return 'rejected';
      }

      // fetch() itself threw (TypeError: Failed to fetch, etc.) — no connectivity.
      await updateQueuedSale({ ...sale, status: 'queued', attempts: sale.attempts + 1, lastError: 'No connection to the server.' });
      this.setState({ lastSyncError: 'No connection to the server.' });
      return 'network-failure';
    }
  }

  private setState(patch: Partial<SyncState>) {
    this.state = { ...this.state, ...patch };
    for (const listener of this.listeners) listener(this.state);
  }
}

export const offlineSyncManager = new OfflineSyncManager();
