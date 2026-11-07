namespace EmlakOfisi.WebUI.Helpers
{
    // Ofise özel görünüm bilgileri (CLAUDE.md: ad ve logo koda sabit yazılmaz). appsettings.json "Office" bölümünden okunur;
    // sır değildir. Başka bir ofise kurulumda sadece bu bölüm ve wwwroot/images/brand altındaki dosyalar değişir.
    public class OfficeSettings
    {
        public const string SectionName = "Office";
        public const string DefaultName = "Emlak Ofisi";

        public string Name { get; set; } = DefaultName;        // ör. "Örnek Gayrimenkul": sayfa başlığı, manifest
        public string ShortName { get; set; } = string.Empty;  // ör. "Örnek": telefon ana ekranındaki ad (boşsa Name)
        public string? LogoPath { get; set; }                  // renkli logo (giriş ve hata sayfası)
        public string? LogoWhitePath { get; set; }             // koyu menü için beyaz yazılı logo
        public string? Icon64Path { get; set; }                // sekme ikonu
        public string? Icon192Path { get; set; }               // ana ekran / apple-touch-icon
        public string? Icon512Path { get; set; }               // PWA büyük ikon

        // Eksik/geçersiz değerleri güvenli hale getirir: ad boşsa varsayılan, kısa ad boşsa ad.
        // Yollar sadece site içinden ("/..." ya da "~/...") olabilir; dış adres ve "//host" yok sayılır (logo yerine ad yazılır).
        public OfficeSettings Normalize()
        {
            Name = string.IsNullOrWhiteSpace(Name) ? DefaultName : Name.Trim();
            ShortName = string.IsNullOrWhiteSpace(ShortName) ? Name : ShortName.Trim();
            LogoPath = LocalPath(LogoPath);
            LogoWhitePath = LocalPath(LogoWhitePath);
            Icon64Path = LocalPath(Icon64Path);
            Icon192Path = LocalPath(Icon192Path);
            Icon512Path = LocalPath(Icon512Path);
            return this;
        }

        public static string? LocalPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            var p = path.Trim();
            if (p.StartsWith("~/", StringComparison.Ordinal))
                p = p[1..];
            // Boşluk ve kontrol karakteri yok: tarayıcı sekme/satır sonunu silip "/<TAB>/host" yolunu "//host" yapabilir
            if (p.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
                return null;
            if (!p.StartsWith('/') || p.StartsWith("//", StringComparison.Ordinal) || p.Contains('\\') || p.Contains(':'))
                return null;
            return p;
        }
    }
}
