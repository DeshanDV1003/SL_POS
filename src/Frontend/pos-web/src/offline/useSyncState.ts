import { useEffect, useState } from 'react';
import { offlineSyncManager, type SyncState } from './syncManager';

const initialState: SyncState = { isOnline: navigator.onLine, isSyncing: false, pendingCount: 0, failedCount: 0, lastSyncError: null };

/** Starts the (singleton, app-lifetime) background sync manager on first use and re-renders whenever its state changes. */
export function useOfflineSyncState(): SyncState {
  const [state, setState] = useState<SyncState>(initialState);

  useEffect(() => {
    offlineSyncManager.start();
    return offlineSyncManager.subscribe(setState);
  }, []);

  return state;
}
