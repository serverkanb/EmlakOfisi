// Emlak Ofisi - küçük yardımcı betikler

// Sadece rakam girilebilen alanlar (telefon): <input data-digits-only ...>
// Yazılan veya yapıştırılan harf/simge anında silinir. Asıl kontrol sunucuda da yapılır.
document.addEventListener('input', function (e) {
    var input = e.target;
    if (!input.matches || !input.matches('[data-digits-only]')) {
        return;
    }

    var digits = input.value.replace(/[^0-9]/g, '');
    if (digits !== input.value) {
        input.value = digits;
    }
});

// Portföy fotoğrafı yükleme (görev 13): <form data-photo-upload>
// Kamera ya da galeriden seçilen fotoğraflar telefonda SIRAYLA küçültülür (en uzun kenar 1600 px, JPEG;
// aynı anda açılan büyük fotoğraflar düşük bellekli telefonda sekmeyi çökertebilir), sonra tek seferde gönderilir.
// Küçültülen dosya yeniden kodlandığı için konum (GPS) gibi EXIF bilgileri de silinir. Küçültme başarısız olursa
// (tarayıcının açamadığı biçim, bellek yetersizliği vb.) orijinal dosya gönderilir: o dosyada EXIF bilgisi kalır,
// sunucu yine boyut ve tip kontrolü yapar.
(function () {
    var MAX_SIDE = 1600;
    var QUALITY = 0.82;
    var MAX_REQUEST_BYTES = 28 * 1024 * 1024; // IIS ve Kestrel'in varsayılan istek sınırı ~30 MB

    function resize(file) {
        return new Promise(function (resolve) {
            if (!file.type || file.type.indexOf('image/') !== 0) { resolve(file); return; }
            var url = URL.createObjectURL(file);
            var img = new Image();
            img.onload = function () {
                var canvas = document.createElement('canvas');
                try {
                    var w = img.naturalWidth, h = img.naturalHeight;
                    var scale = Math.min(1, MAX_SIDE / Math.max(w, h));
                    canvas.width = Math.max(1, Math.round(w * scale));
                    canvas.height = Math.max(1, Math.round(h * scale));
                    var ctx = canvas.getContext('2d');
                    ctx.fillStyle = '#fff'; // saydam PNG (kat planı vb.) JPEG'de siyah olmasın
                    ctx.fillRect(0, 0, canvas.width, canvas.height);
                    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
                    canvas.toBlob(function (blob) {
                        canvas.width = 0; canvas.height = 0; // belleği hemen bırak (iOS canvas sınırı)
                        if (!blob) { resolve(file); return; }
                        var name = (file.name || 'foto').replace(/\.[^.]+$/, '') + '.jpg';
                        resolve(new File([blob], name, { type: 'image/jpeg' }));
                    }, 'image/jpeg', QUALITY);
                } catch (err) {
                    canvas.width = 0; canvas.height = 0;
                    resolve(file);
                } finally {
                    URL.revokeObjectURL(url);
                }
            };
            img.onerror = function () { URL.revokeObjectURL(url); resolve(file); };
            img.src = url;
        });
    }

    // Dosyaları birer birer küçült (Promise zinciri)
    function resizeAll(files, onProgress) {
        var results = [];
        return files.reduce(function (chain, file, i) {
            return chain.then(function () {
                onProgress(i + 1, files.length);
                return resize(file).then(function (r) { results.push(r); });
            });
        }, Promise.resolve()).then(function () { return results; });
    }

    function setBusy(form, busy) {
        form.querySelectorAll('label, button').forEach(function (el) { el.classList.toggle('disabled', busy); });
    }

    document.addEventListener('change', function (e) {
        var picker = e.target;
        if (!picker.matches || !picker.matches('[data-photo-picker]') || !picker.files || picker.files.length === 0) {
            return;
        }

        var form = picker.closest('form[data-photo-upload]');
        var target = form.querySelector('[data-photo-target]');
        var status = form.querySelector('[data-photo-status]');
        var max = parseInt(form.getAttribute('data-max-files'), 10) || 10;
        var all = Array.prototype.slice.call(picker.files);
        var files = all.slice(0, max);
        var note = all.length > max ? ' (' + all.length + ' fotoğraf seçildi, ilk ' + max + ' tanesi yükleniyor)' : '';

        setBusy(form, true);
        resizeAll(files, function (i, n) {
            if (status) { status.textContent = 'Fotoğraflar hazırlanıyor: ' + i + ' / ' + n + note; }
        }).then(function (resized) {
            var total = resized.reduce(function (sum, f) { return sum + f.size; }, 0);
            if (total > MAX_REQUEST_BYTES) {
                if (status) { status.textContent = 'Seçilen fotoğraflar çok büyük. Lütfen daha az fotoğrafla tekrar deneyin.'; }
                setBusy(form, false);
                picker.value = '';
                return;
            }
            try {
                var dt = new DataTransfer();
                resized.forEach(function (f) { dt.items.add(f); });
                target.files = dt.files;
            } catch (err) {
                // Eski tarayıcı: küçültmeden, seçilen dosyalarla gönder
                target.removeAttribute('name');
                picker.setAttribute('name', 'files');
            }
            if (status) { status.textContent = 'Yükleniyor, lütfen bekleyin…' + note; }
            form.submit();
        });
    });

    // Geri tuşuyla sayfa önbellekten dönünce butonlar pasif kalmasın
    window.addEventListener('pageshow', function () {
        document.querySelectorAll('form[data-photo-upload]').forEach(function (form) { setBusy(form, false); });
    });
})();

// Arsa seçilince arsada anlamsız alanlar (oda sayısı, kat, bina yaşı) gizlenir ve gönderilmez.
// <select data-property-type> ... <div data-hide-for-land>. Sunucu da arsada bu alanları kaydetmez.
// Alan girişinde dönüm karşılığı: <input data-area-hint> + <div data-area-hint-text>.
(function () {
    'use strict';

    function apply(select) {
        var form = select.form || document;
        var isLand = select.value === 'Land';
        form.querySelectorAll('[data-hide-for-land]').forEach(function (box) {
            box.hidden = isLand;
            box.querySelectorAll('input, select, textarea').forEach(function (input) {
                input.disabled = isLand;          // gizli alan gönderilmez; değer silinmez, yanlışlıkla Arsa seçilip geri dönülürse kaybolmasın
            });
        });
    }

    document.querySelectorAll('select[data-property-type]').forEach(function (select) {
        apply(select);
        select.addEventListener('change', function () { apply(select); });
    });

    function areaText(value) {
        var m2 = parseInt(value, 10);
        if (!m2 || m2 <= 0) return '1 dönüm = 1.000 m²';
        if (m2 < 1000) return m2.toLocaleString('tr-TR') + ' m²';
        var donum = Math.round(m2 / 10) / 100;
        return m2.toLocaleString('tr-TR') + ' m² = ' + donum.toLocaleString('tr-TR') + ' dönüm';
    }

    document.querySelectorAll('input[data-area-hint]').forEach(function (input) {
        var box = input.closest('[class*="col-"]') || input.parentElement;
        var hint = box ? box.querySelector('[data-area-hint-text]') : null;
        if (!hint) return;
        var update = function () { hint.textContent = areaText(input.value); };
        update();
        input.addEventListener('input', update);
    });
})();
