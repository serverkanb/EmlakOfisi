// Bas-konuş mikrofon düğmesi (görev 16b). Sadece data-mic işaretli TEXTAREA'larda çalışır (serbest metin notları).
// Web Speech API, dil tr-TR; harici paket yok. Not: tarayıcı (Chrome/Safari) sesi metne çevirmek için kendi
// üreticisinin sunucusuna gönderir. Tarayıcı desteklemiyorsa ya da bağlantı güvenli değilse düğme hiç eklenmez.
//
// Durumlar: hazır -> hazırlanıyor (start çağrıldı) -> dinleniyor (onstart) -> işleniyor (stop çağrıldı) -> hazır (onend).
// İşlenirken tekrar basılırsa basış not edilir ve oturum bitince yeniden başlar. onend hiç gelmezse emniyet zamanlayıcısı
// oturumu kapatır (düğme kilitli kalmasın).
(function () {
    'use strict';

    var Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!Recognition || !window.isSecureContext) return;

    var STOP_TIMEOUT_MS = 3000;
    var active = null;   // aynı anda tek alan dinlenir

    function setStatus(state, text, isError) {
        state.status.textContent = text || '';
        state.status.classList.toggle('text-danger', !!isError);
    }

    function setLook(state, look) {
        state.button.classList.toggle('mic-preparing', look === 'preparing');
        state.button.classList.toggle('mic-listening', look === 'listening');
        state.button.classList.toggle('mic-processing', look === 'processing');
    }

    // Metni alanın sonuna ekler (gerekirse araya boşluk), karakter sınırını aşmaz, doğrulama için input olayı yollar.
    // Dönüş: 'none' | 'added' | 'trimmed'
    function appendText(textarea, text) {
        text = (text || '').replace(/\s+/g, ' ').trim();
        if (!text) return 'none';
        var current = textarea.value;
        var joined = current.length === 0 || /\s$/.test(current) ? current + text : current + ' ' + text;
        // maxlength yoksa form doğrulamasının sınırı (StringLength -> data-val-length-max) kullanılır
        var max = textarea.maxLength > 0 ? textarea.maxLength : (parseInt(textarea.getAttribute('data-val-length-max'), 10) || 0);
        var trimmed = false;
        if (max && joined.length > max) { joined = joined.slice(0, max); trimmed = true; }
        textarea.value = joined;
        textarea.dispatchEvent(new Event('input', { bubbles: true }));
        return trimmed ? 'trimmed' : 'added';
    }

    function errorText(code) {
        switch (code) {
            case 'not-allowed': return 'Mikrofon izni verilmedi.';
            case 'service-not-allowed': return 'Bu cihazda sesle yazma kullanılamıyor (Dikte kapalı olabilir).';
            case 'no-speech': return 'Ses algılanamadı.';
            case 'audio-capture': return 'Mikrofon bulunamadı.';
            case 'network': return 'İnternet bağlantısı gerekli.';
            case 'aborted': return null;
            default: return 'Ses tanınamadı, tekrar deneyin.';
        }
    }

    function start(state) {
        state.pressed = true;
        if (active && active !== state) stop(active);
        if (state.phase !== 'idle') return;   // işleniyorsa: oturum bitince yeniden başlar (pressed)

        var rec = new Recognition();
        rec.lang = 'tr-TR';
        rec.continuous = true;
        rec.interimResults = false;
        state.rec = rec;
        state.finalText = '';
        state.error = null;
        state.shortTap = false;
        state.phase = 'preparing';
        state.startedAt = Date.now();
        active = state;

        rec.onstart = function () {
            if (state.rec !== rec || state.phase !== 'preparing') return;
            state.phase = 'listening';
            setLook(state, 'listening');
            setStatus(state, 'Dinleniyor…');
        };
        // Sonuç listesi her olayda baştan okunur (birikerek gelen listede aynı metin iki kez eklenmesin)
        rec.onresult = function (e) {
            var text = '';
            for (var i = 0; i < e.results.length; i++) {
                if (e.results[i].isFinal) text += ' ' + e.results[i][0].transcript;
            }
            state.finalText = text;
        };
        rec.onerror = function (e) { state.error = errorText(e.error); };
        rec.onend = function () { if (state.rec === rec) end(state); };

        setLook(state, 'preparing');
        setStatus(state, 'Hazırlanıyor…');
        try {
            rec.start();
        } catch (err) {
            state.error = 'Ses tanınamadı, tekrar deneyin.';
            end(state);
        }
    }

    function stop(state) {
        state.pressed = false;
        if (state.phase !== 'preparing' && state.phase !== 'listening') return;
        state.shortTap = Date.now() - state.startedAt < 400;
        state.phase = 'processing';
        setLook(state, 'processing');
        setStatus(state, 'İşleniyor…');
        var rec = state.rec;
        try { rec.stop(); } catch (err) { /* zaten durmuş */ }
        // onend gelmezse (bazı cihazlarda) düğme kilitli kalmasın
        state.timer = setTimeout(function () {
            if (state.rec !== rec) return;
            try { rec.abort(); } catch (err) { /* yok say */ }
            end(state);
        }, STOP_TIMEOUT_MS);
    }

    function end(state) {
        clearTimeout(state.timer);
        var result = appendText(state.textarea, state.finalText);
        state.rec = null;
        state.phase = 'idle';
        setLook(state, null);
        if (active === state) active = null;

        if (state.error) setStatus(state, state.error, true);
        else if (result === 'trimmed') setStatus(state, 'Karakter sınırına ulaşıldı.', true);
        else if (result === 'none' && state.shortTap) setStatus(state, 'Konuşmak için basılı tutun.');
        else setStatus(state, '');

        // İşlenirken tekrar basıldıysa ve hâlâ basılıysa yeniden dinle
        if (state.pressed && !state.error) start(state);
    }

    function attach(textarea) {
        if (textarea.tagName !== 'TEXTAREA' || textarea.dataset.micReady) return;
        textarea.dataset.micReady = '1';

        var label = textarea.id ? document.querySelector('label[for="' + textarea.id + '"]') : null;
        var fieldName = label ? label.textContent.trim() : 'not';

        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn mic-button';
        button.setAttribute('aria-label', fieldName + ' alanına sesle yaz: basılı tutarak konuşun');
        button.title = 'Basılı tutarak konuşun';
        button.innerHTML = '<i class="ti ti-microphone" aria-hidden="true"></i>';

        var status = document.createElement('div');
        status.className = 'mic-status small text-secondary';
        status.setAttribute('aria-live', 'polite');

        var bar = document.createElement('div');
        bar.className = 'mic-bar';
        bar.appendChild(status);
        bar.appendChild(button);
        textarea.insertAdjacentElement('afterend', bar);

        var state = { textarea: textarea, button: button, status: status, phase: 'idle', pressed: false };

        // Dokunmatik ve fare: basılı tut / bırak. Parmak kayarsa ya da sistem iptal ederse de durur.
        button.addEventListener('pointerdown', function (e) {
            if (e.button !== undefined && e.button !== 0) return;
            e.preventDefault();
            try { button.setPointerCapture(e.pointerId); } catch (err) { /* desteklenmiyor */ }
            start(state);
        });
        ['pointerup', 'pointercancel', 'lostpointercapture'].forEach(function (name) {
            button.addEventListener(name, function () { stop(state); });
        });
        button.addEventListener('contextmenu', function (e) { e.preventDefault(); });   // uzun basışta menü açılmasın

        // Ekran okuyucu (VoiceOver/TalkBack çift dokunma) ve klavye "click"i basılı tutamaz: aç/kapa gibi çalışır
        button.addEventListener('click', function (e) {
            if (e.detail !== 0 || state.keyHeld) return;   // fare/parmak tıklaması: bas-konuş zaten işledi
            if (state.phase === 'idle') start(state); else stop(state);
        });

        // Klavye: Boşluk ya da Enter basılı tutulunca dinler, bırakınca durur
        button.addEventListener('keydown', function (e) {
            if ((e.key === ' ' || e.key === 'Enter') && !e.repeat) { e.preventDefault(); state.keyHeld = true; start(state); }
        });
        button.addEventListener('keyup', function (e) {
            // keyHeld bir sonraki turda bırakılır: tuştan sonra gelebilecek "click" aç/kapa sanılmasın
            if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); stop(state); setTimeout(function () { state.keyHeld = false; }, 0); }
        });
        button.addEventListener('blur', function () { if (state.keyHeld) { stop(state); state.keyHeld = false; } });
    }

    document.querySelectorAll('textarea[data-mic]').forEach(attach);

    // Sayfa arka plana giderse (telefon kilitlenir, sekme değişir) dinleme kapansın
    document.addEventListener('visibilitychange', function () {
        if (document.hidden && active) stop(active);
    });
})();
