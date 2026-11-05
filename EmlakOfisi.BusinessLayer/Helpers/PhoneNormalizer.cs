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
