/* TelecallingCRM Service Worker — PWA Support */
const CACHE_NAME = 'telecrm-v1';

const STATIC_ASSETS = [
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/css/site.css',
    '/js/site.js'
];

/* ?? Install: pre-cache static assets ??????????????????????????? */
self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(STATIC_ASSETS))
            .catch(() => { /* silently ignore cache failures on first install */ })
    );
    self.skipWaiting();
});

/* ?? Activate: remove stale caches ?????????????????????????????? */
self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

/* ?? Fetch strategy ????????????????????????????????????????????? */
self.addEventListener('fetch', event => {
    const { request } = event;
    const url = new URL(request.url);

    // Only handle GET requests from same origin (or trusted CDN)
    if (request.method !== 'GET') return;
    const sameOrigin = url.origin === self.location.origin;
    const trustedCdn = url.hostname === 'cdn.jsdelivr.net' || url.hostname === 'cdnjs.cloudflare.com';
    if (!sameOrigin && !trustedCdn) return;

    // API / SignalR: network-only (no caching)
    if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) return;

    // Static assets (css, js, fonts, images): cache-first, then network
    if (/\.(css|js|woff2?|ttf|eot|png|jpg|jpeg|svg|ico|webp)$/i.test(url.pathname) || trustedCdn) {
        event.respondWith(
            caches.match(request).then(cached => {
                if (cached) return cached;
                return fetch(request).then(response => {
                    if (response.ok) {
                        const clone = response.clone();
                        caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
                    }
                    return response;
                });
            })
        );
        return;
    }

    // HTML navigation: network-first, fall back to cache
    event.respondWith(
        fetch(request)
            .then(response => {
                if (response.ok) {
                    const clone = response.clone();
                    caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
                }
                return response;
            })
            .catch(() => caches.match(request))
    );
});
