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
