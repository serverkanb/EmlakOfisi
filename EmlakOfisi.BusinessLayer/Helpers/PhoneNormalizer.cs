namespace EmlakOfisi.BusinessLayer.Helpers
{
    // Telefon numarasını karşılaştırılabilir hale getirir: sadece rakamlar, 90 / 0 öneki atılmış.
    // "0532 111 22 33", "+90 (532) 111-22-33", "5321112233" -> "5321112233"
    public static class PhoneNormalizer
    {
        public static string Normalize(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());

            if (digits.Length == 12 && digits.StartsWith("90"))
                digits = digits[2..];
            else if (digits.Length == 11 && digits.StartsWith("0"))
                digits = digits[1..];

            return digits.Length > 10 ? digits[^10..] : digits;
        }

        // Formdan girilen numara geçerli bir Türkiye numarası mı? Kırpmadan ÖNCE hane sayısına bakar:
        // 10 hane, 0 ile başlayan 11 hane veya 90 ile başlayan 12 hane kabul edilir.
        // ("0532 111 22 333" gibi fazla haneli numara sessizce kırpılıp kabul edilmesin.)
        public static bool IsValid(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            // Formdaki telefon sadece rakam (ve okunurluk için boşluk) içerebilir; harf/simge kabul edilmez
            if (!HasOnlyDigits(phone))
                return false;

            var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
            return digits.Length switch
            {
                10 => true,
                11 => digits.StartsWith("0"),
                12 => digits.StartsWith("90"),
                _ => false
            };
        }

        // Sadece rakam ve boşluk var mı? (eski kayıtlarda "0532 111 22 33" gibi boşluklu yazım duruyor)
        public static bool HasOnlyDigits(string phone) =>
            phone.All(ch => char.IsAsciiDigit(ch) || ch == ' ');

        // Arama kutusundaki ifade telefon araması mı? (en az 3 rakam, harf yok)
        public static bool LooksLikePhone(string search)
        {
            var hasLetter = search.Any(char.IsLetter);
            var digitCount = search.Count(char.IsAsciiDigit);
            return !hasLetter && digitCount >= 3;
        }

        // Arama için: baştaki 90 / 0 atılır ama kısa (kısmi) aramalar kısaltılmaz.
        public static string NormalizeForSearch(string search)
        {
            var digits = new string(search.Where(char.IsAsciiDigit).ToArray());

            if (digits.StartsWith("90") && digits.Length > 10)
                digits = digits[2..];
            if (digits.StartsWith("0"))
                digits = digits[1..];

            return digits;
        }

        // WhatsApp bağlantısı için ülke kodlu numara (wa.me/905321112233)
        public static string ToWhatsApp(string phoneNormalized) =>
            phoneNormalized.Length == 10 ? "90" + phoneNormalized : phoneNormalized;
    }
}
