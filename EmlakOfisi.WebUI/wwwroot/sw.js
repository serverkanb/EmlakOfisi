// Service worker (görev 16, PWA). Amaç: ana ekrana eklenebilme ve bağlantı yokken Türkçe uyarı sayfası.
// GÜVENLİK KURALI: önbelleğe SADECE statik dosyalar (/lib, /css, /js, /images) alınır.
// Sayfalar, formlar, fotoğraflar (/PropertyPhoto/Show) ve diğer tüm uçlar HİÇ saklanmaz; her seferinde sunucudan gelir.
// Böylece ortak ya da kaybolan bir telefonda müşteri/portföy bilgisi kalmaz. Offline çalışma yoktur.
// Bu dosya, site.css ya da logo değişince CACHE_NAME sürümü artırılır (uyarı sayfası sürümsüz kopyaları kullanır);
// eski önbellek "activate" adımında silinir. Uygulama sitenin KÖKÜNDE çalışır varsayımıyla yazıldı (/sw.js, /lib/ ...).

const CACHE_NAME = 'emlakofisi-statik-v3';
const OFFLINE_URL = '/offline.html';
const STATIC_PREFIXES = ['/lib/', '/css/', '/js/', '/images/'];

// Bağlantı yokken uyarı sayfasının kendisi ve görünümü için gerekenler
const PRECACHE_URLS = [
    OFFLINE_URL,
    '/lib/tabler/css/tabler.min.css',
    '/css/site.css',
    '/images/brand/altinkule-logo.png'
];

function isStatic(url) {
    return url.origin === self.location.origin && STATIC_PREFIXES.some((p) => url.pathname.startsWith(p));
}

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => cache.addAll(PRECACHE_URLS))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    const request = event.request;

    // POST (form gönderme, çıkış vb.) ve başka sitelere giden istekler service worker'a hiç takılmaz
    if (request.method !== 'GET') return;
    const url = new URL(request.url);
    if (url.origin !== self.location.origin) return;

    // Sayfa açılışı: her zaman sunucudan; SAKLANMAZ. Bağlantı yoksa uyarı sayfası.
    if (request.mode === 'navigate') {
        event.respondWith(fetch(request).catch(() => caches.match(OFFLINE_URL)));
        return;
    }

    // Statik dosya: önbellekte varsa hemen ver, arkada güncelle (eski sürümü aynı yoldan silerek)
    if (isStatic(url)) {
        event.respondWith(
            caches.open(CACHE_NAME).then(async (cache) => {
                const cached = await cache.match(request);
                const network = fetch(request).then(async (response) => {
                    if (response.status === 200 && response.type === 'basic') {
                        // Önbelleğe yazılamasa da (depolama dolu vb.) sunucudan gelen yanıt sayfaya ulaşır
                        try {
                            // Sürümlü dosyanın (?v=...) eski sürümleri silinir. Sürümsüz kopyalar (uyarı sayfasının kullandıkları) korunur.
                            if (url.search !== '') {
                                const old = await cache.keys();
                                await Promise.all(old
                                    .filter((r) => { const u = new URL(r.url); return u.pathname === url.pathname && u.search !== '' && u.search !== url.search; })
                                    .map((r) => cache.delete(r)));
                            }
                            await cache.put(request, response.clone());
                        } catch (e) { /* önbellek hatası yanıtı bozmasın */ }
                    }
                    return response;
                });

                if (cached) {
                    event.waitUntil(network.catch(() => undefined));
                    return cached;
                }
                return network;
            })
        );
    }

    // Diğer her şey (fotoğraflar, uçlar): service worker karışmaz, tarayıcı normal şekilde ister
});
