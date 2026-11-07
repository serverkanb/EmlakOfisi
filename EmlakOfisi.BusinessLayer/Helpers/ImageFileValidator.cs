namespace EmlakOfisi.BusinessLayer.Helpers
{
    // Yüklenen dosyanın gerçekten resim olduğunu içeriğinin ilk baytlarından anlar.
    // Dosya adı uzantısına ve tarayıcının söylediği tipe güvenilmez (ör. "foto.jpg" adlı bir HTML dosyası).
    public static class ImageFileValidator
    {
        public const int MaxBytes = 5 * 1024 * 1024; // 5 MB (telefonda küçültülmüş foto genelde 200-600 KB)

        // Desteklenen tipler: (içerik tipi, sunucuda kullanılacak uzantı). Tanınmazsa null.
        public static (string ContentType, string Extension)? Detect(ReadOnlySpan<byte> header)
        {
            if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return ("image/jpeg", ".jpg");

            if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
                return ("image/png", ".png");

            // RIFF....WEBP
            if (header.Length >= 12 &&
                header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
                header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
                return ("image/webp", ".webp");

            return null;
        }
    }
}
