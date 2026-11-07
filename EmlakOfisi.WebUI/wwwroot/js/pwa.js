// PWA (görev 16): service worker kaydı. sw.js sadece statik dosyaları önbelleğe alır; sayfa ve müşteri verisi saklanmaz.
// Tarayıcı desteklemiyorsa ya da bağlantı HTTPS değilse (localhost hariç) kayıt yapılmaz; uygulama normal çalışır.
(function () {
    'use strict';
    if (!('serviceWorker' in navigator)) return;
    window.addEventListener('load', function () {
        navigator.serviceWorker.register('/sw.js').catch(function () { /* sessizce geç */ });
    });
})();
