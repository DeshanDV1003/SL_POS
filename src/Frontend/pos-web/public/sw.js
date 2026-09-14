// Minimal, hand-rolled service worker — app-shell caching only, no library
// (workbox/vite-plugin-pwa) since one cache + one fetch strategy doesn't need one.
// See docs/architecture.md §10: the offline sales *queue* itself lives in
// IndexedDB and is driven entirely by the page's own JS (src/offline/), not by
// this worker — this worker's only job is letting the page shell load at all when
// the terminal opens the app while offline.
const CACHE_NAME = 'universalpos-shell-v1';
const APP_SHELL = ['/', '/index.html', '/favicon.svg'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(APP_SHELL)),
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))),
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // Never intercept API traffic — that's the offline queue's job (src/offline/),
  // which needs to see real success/failure from the network, not a cached lie.
  if (url.pathname.startsWith('/api/')) return;
  if (event.request.method !== 'GET') return;

  event.respondWith(
    fetch(event.request)
      .then((response) => {
        const copy = response.clone();
        caches.open(CACHE_NAME).then((cache) => cache.put(event.request, copy));
        return response;
      })
      .catch(() => caches.match(event.request).then((cached) => cached ?? caches.match('/index.html'))),
  );
});
