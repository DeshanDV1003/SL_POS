/**
 * A minimal, dependency-free IndexedDB wrapper for the offline sales queue — see
 * docs/architecture.md §10. Deliberately hand-rolled rather than pulling in a
 * library (idb/dexie) for one object store with three simple operations.
 */

const DB_NAME = 'universalpos-offline';
const DB_VERSION = 1;
export const PENDING_SALES_STORE = 'pendingSales';

let dbPromise: Promise<IDBDatabase> | null = null;

function openDb(): Promise<IDBDatabase> {
  if (dbPromise) return dbPromise;

  dbPromise = new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(PENDING_SALES_STORE)) {
        db.createObjectStore(PENDING_SALES_STORE, { keyPath: 'clientIdempotencyKey' });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });

  return dbPromise;
}

export async function withStore<T>(mode: IDBTransactionMode, fn: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const db = await openDb();
  return new Promise<T>((resolve, reject) => {
    const tx = db.transaction(PENDING_SALES_STORE, mode);
    const store = tx.objectStore(PENDING_SALES_STORE);
    const request = fn(store);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export async function getAllFromStore<T>(): Promise<T[]> {
  return withStore<T[]>('readonly', (store) => store.getAll() as IDBRequest<T[]>);
}
