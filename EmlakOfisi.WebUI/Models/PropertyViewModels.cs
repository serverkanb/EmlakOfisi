using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.WebUI.Models
{
    // Portföy listesindeki bir satır/kart. Entity view'a verilmez (AgentUser tam AppUser satırı taşır).
    public class PropertyListItemViewModel
    {
        public int PropertyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public ListingType ListingType { get; set; }
        public PropertyType PropertyType { get; set; }
        public PropertyStatus Status { get; set; }
        public decimal Price { get; set; }
        public string Location { get; set; } = string.Empty;   // "Kadıköy / İstanbul"
        public string? RoomCount { get; set; }
        public int? NetArea { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public Visibility Visibility { get; set; }
        public bool IsMine { get; set; }                        // oluşturan veya sorumlu

        public string PriceText => PropertyLabels.Price(Price, ListingType);
    }

    public class PropertyListViewModel
    {
        public List<PropertyListItemViewModel> Items { get; set; } = new();

        // Filtreler (adreste taşınır)
        public string? Q { get; set; }
        public ListingType? ListingType { get; set; }
        public PropertyType? PropertyType { get; set; }
        public PropertyStatus? Status { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? MinArea { get; set; }
        public bool Mine { get; set; }

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasFilter => !string.IsNullOrWhiteSpace(Q) || ListingType != null || PropertyType != null ||
                                 Status != null || MinPrice != null || MaxPrice != null || MinArea != null || Mine;
    }

    // Ekleme / düzenleme formu. Sahip, tarih, silinme durumu ve id alanları YOK (over-posting kapalı).
    // Fiyat ve konum metin olarak alınır: tarayıcının ondalık biçimi (nokta) ile sunucu kültürü (virgül)
    // karışmasın diye fiyat sadece rakam (tam TL), konum "enlem, boylam" (nokta ile) olarak okunur.
    public class PropertyFormViewModel
    {
        [Required(ErrorMessage = "Başlık zorunludur.")]
        [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
        [Display(Name = "Başlık")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "İlan tipi")]
        public ListingType ListingType { get; set; } = ListingType.Sale;

        [Display(Name = "Gayrimenkul türü")]
        public PropertyType PropertyType { get; set; } = PropertyType.Residence;

        [Display(Name = "Durum")]
        public PropertyStatus Status { get; set; } = PropertyStatus.Active;

        [Required(ErrorMessage = "Fiyat zorunludur.")]
        [RegularExpression("^[0-9 .]{1,15}$", ErrorMessage = "Fiyata sadece rakam girin (ör. 5000000).")]
        [Display(Name = "Fiyat (TL)")]
        public string PriceText { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şehir zorunludur.")]
        [StringLength(50)]
        [Display(Name = "Şehir")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "İlçe zorunludur.")]
        [StringLength(50)]
        [Display(Name = "İlçe")]
        public string District { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Mahalle")]
        public string? Neighborhood { get; set; }

        [StringLength(500)]
        [Display(Name = "Adres")]
        public string? Address { get; set; }

        [StringLength(60)]
        [Display(Name = "Konum (enlem, boylam)")]
        public string? LocationText { get; set; }

        [Range(0, 100000, ErrorMessage = "Brüt alan 0 ile 100000 arasında olmalıdır.")]
        [Display(Name = "Brüt m²")]
        public int? GrossArea { get; set; }

        [Range(0, 100000, ErrorMessage = "Net alan 0 ile 100000 arasında olmalıdır.")]
        [Display(Name = "Net m²")]
        public int? NetArea { get; set; }

        [StringLength(10)]
        [RegularExpression(@"^[0-9]{1,2}(\+[0-9]{1,2})?$", ErrorMessage = "Oda sayısını 3+1 biçiminde girin.")]
        [Display(Name = "Oda sayısı")]
        public string? RoomCount { get; set; }

        [Range(-5, 200, ErrorMessage = "Kat -5 ile 200 arasında olmalıdır.")]
        [Display(Name = "Kat")]
        public int? Floor { get; set; }

        [Range(0, 300, ErrorMessage = "Bina yaşı 0 ile 300 arasında olmalıdır.")]
        [Display(Name = "Bina yaşı")]
        public int? BuildingAge { get; set; }

        [StringLength(4000, ErrorMessage = "Açıklama en fazla 4000 karakter olabilir.")]
        [Display(Name = "Açıklama")]
        public string? Description { get; set; }

        [StringLength(500, ErrorMessage = "İlan linki en fazla 500 karakter olabilir.")]
        [Display(Name = "İlan linki")]
        public string? ListingUrl { get; set; }

        [Display(Name = "Mülk sahibi")]
        public int? OwnerContactId { get; set; }

        // Sadece yöneticiye gösterilir; danışmanın gönderdiği değer serviste yok sayılır
        [Display(Name = "Sorumlu danışman")]
        public int AgentUserId { get; set; }

        [Display(Name = "Kim görebilir?")]
        public Visibility Visibility { get; set; } = Visibility.OnlyMe;
    }

    // Formun seçenekleri (controller doldurur)
    public class PropertyFormOptions
    {
        public bool CanChooseAgent { get; set; }
        public List<(int Id, string Name)> Agents { get; set; } = new();
        public List<(int Id, string Name)> Owners { get; set; } = new();  // kullanıcının görebildiği kişiler
        public bool OwnerHidden { get; set; }                               // mevcut sahip görünmüyor: değiştirilemez
        public int? PropertyId { get; set; }                                // düzenlemede
    }

    public class PropertyDetailViewModel
    {
        public PropertyListItemViewModel Summary { get; set; } = new();
        public string? Neighborhood { get; set; }
        public string? Address { get; set; }
        public int? GrossArea { get; set; }
        public int? Floor { get; set; }
        public int? BuildingAge { get; set; }
        public string? Description { get; set; }
        public string? MapLink { get; set; }
        public string? ListingUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Mülk sahibi: görünüyorsa adı/telefonu; görünmüyorsa sadece "Gizli kayıt" (id/ad/telefon YOK)
        public bool HasOwner { get; set; }
        public bool OwnerHidden { get; set; }
        public int? OwnerContactId { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerPhone { get; set; }
        public string? OwnerTelLink { get; set; }

        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        // Görev 12c: ilgilenen müşteriler
        public InterestPanelViewModel Interests { get; set; } = new();

        // Görev 13: fotoğraflar (kapak önce). Yükleme/silme/kapak butonları CanEdit olana gösterilir.
        public List<PropertyPhotoItemViewModel> Photos { get; set; } = new();

        // Görev 18: satış/kiralama işlemleri (sadece görebildiği)
        public TransactionPanelViewModel Transactions { get; set; } = new();
    }

    // Fotoğraf: sadece id (dosya, yetki kontrollü PropertyPhoto/Show/{id} ucundan gelir; dosya adı/yolu view'a verilmez)
    public class PropertyPhotoItemViewModel
    {
        public int PropertyPhotoId { get; set; }
        public bool IsCover { get; set; }
    }

    // Etiketler, fiyat ve konum biçimleri tek yerde
    public static class PropertyLabels
    {
        public static readonly CultureInfo Tr = new("tr-TR");

        public static string Listing(ListingType t) => t == ListingType.Rent ? "Kiralık" : "Satılık";

        public static string Type(PropertyType t) => t switch
        {
            PropertyType.Residence => "Konut",
            PropertyType.Land => "Arsa",
            PropertyType.Commercial => "İş yeri",
            _ => "Diğer"
        };

        public static string Status(PropertyStatus s) => s switch
        {
            PropertyStatus.Active => "Aktif",
            PropertyStatus.Passive => "Pasif",
            PropertyStatus.Sold => "Satıldı",
            PropertyStatus.Rented => "Kiralandı",
            _ => s.ToString()
        };

        public static string StatusBadge(PropertyStatus s) => s switch
        {
            PropertyStatus.Active => "bg-green-lt",
            PropertyStatus.Passive => "bg-secondary-lt",
            PropertyStatus.Sold => "bg-red-lt",
            PropertyStatus.Rented => "bg-orange-lt",
            _ => "bg-secondary-lt"
        };

        // 5.000.000 ₺  /  kiralıkta "40.000 ₺ / ay"
        public static string Price(decimal price, ListingType listing) =>
            price.ToString("N0", Tr) + " ₺" + (listing == ListingType.Rent ? " / ay" : "");

        // Alan: "850 m²", 1.000 m² ve üstünde dönüm karşılığıyla "5.000 m² (5 dönüm)", "2.500 m² (2,5 dönüm)"
        public static string Area(int squareMeters) =>
            squareMeters >= 1000
                ? $"{squareMeters.ToString("N0", Tr)} m² ({(squareMeters / 1000m).ToString("0.##", Tr)} dönüm)"
                : $"{squareMeters.ToString("N0", Tr)} m²";

        // Yüzde: "2", "2,5" ya da "2.5"; 0–100 arası, en fazla 2 ondalık (decimal(5,2) ile uyumlu)
        public static bool TryParsePercent(string? text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.Trim().Replace(',', '.');
            return Regex.IsMatch(t, @"^\d{1,3}(\.\d{1,2})?$") &&
                   decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) &&
                   value <= 100;
        }

        // Forma geri yazmak için: 2.50 -> "2,5"
        public static string? PercentText(decimal? value) => value?.ToString("0.##", Tr);

        // Sadece rakam (ve okunurluk için boşluk/nokta) kabul eder: "5.000.000" -> 5000000
        public static bool TryParsePrice(string? text, out decimal price)
        {
            price = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var compact = text.Replace(" ", "");
            // Nokta sadece binlik ayracı olabilir ("5.000.000"); "1.5" ya da "8500000.50" reddedilir,
            // yoksa ondalık kısım sessizce rakamlara katılırdı.
            if (!Regex.IsMatch(compact, @"^(\d+|\d{1,3}(\.\d{3})+)$")) return false;
            var digits = compact.Replace(".", "");
            if (digits.Length > 15) return false;
            return decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out price);
        }

        // "40.98765, 29.03123" (Google Haritalar'dan kopyalanan biçim) -> enlem/boylam
        public static bool TryParseLocation(string? text, out decimal? lat, out decimal? lng)
        {
            lat = null; lng = null;
            if (string.IsNullOrWhiteSpace(text)) return true; // boş: konum yok
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            if (!decimal.TryParse(parts[0], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var la)) return false;
            if (!decimal.TryParse(parts[1], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lo)) return false;
            if (la is < -90 or > 90 || lo is < -180 or > 180) return false;
            lat = Math.Round(la, 6); lng = Math.Round(lo, 6);
            return true;
        }

        public static string? LocationText(decimal? lat, decimal? lng) =>
            lat == null || lng == null ? null
                : $"{lat.Value.ToString(CultureInfo.InvariantCulture)}, {lng.Value.ToString(CultureInfo.InvariantCulture)}";

        // Konum varsa koordinat, yoksa adresle Google Haritalar araması
        public static string? MapLink(decimal? lat, decimal? lng, string? address, string? neighborhood, string district, string city)
        {
            if (lat != null && lng != null)
                return "https://www.google.com/maps?q=" +
                       Uri.EscapeDataString($"{lat.Value.ToString(CultureInfo.InvariantCulture)},{lng.Value.ToString(CultureInfo.InvariantCulture)}");

            var parts = new[] { address, neighborhood, district, city }.Where(p => !string.IsNullOrWhiteSpace(p));
            var query = string.Join(", ", parts);
            return string.IsNullOrWhiteSpace(query) ? null : "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(query);
        }
    }
}
