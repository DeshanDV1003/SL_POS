import { useOfflineSyncState } from '../offline/useSyncState';
import { offlineSyncManager } from '../offline/syncManager';

/** Shows connectivity and any sales still waiting to sync — see docs/architecture.md §10. */
export function OfflineStatusBadge() {
  const state = useOfflineSyncState();

  if (state.isOnline && state.pendingCount === 0 && state.failedCount === 0) {
    return null;
  }

  const label = !state.isOnline
    ? 'Offline — sales will be saved and synced automatically'
    : state.isSyncing
      ? 'Syncing offline sales…'
      : `${state.pendingCount} offline sale${state.pendingCount === 1 ? '' : 's'} waiting to sync`;

  return (
    <div className={`offline-badge ${state.isOnline ? 'offline-badge-online' : 'offline-badge-offline'}`}>
      <span>{label}</span>
      {state.failedCount > 0 && (
        <span className="offline-badge-failed"> · {state.failedCount} rejected on sync (needs review)</span>
      )}
      {state.isOnline && state.pendingCount > 0 && !state.isSyncing && (
        <button type="button" onClick={() => void offlineSyncManager.triggerSync()} className="offline-badge-retry">
          Sync now
        </button>
      )}
    </div>
  );
}
