using System.ComponentModel.DataAnnotations;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.WebUI.Models
{
    // Bir talep satırı/kartı. Entity view'a verilmez.
    public class CustomerRequestItemViewModel
    {
        public int CustomerRequestId { get; set; }
        public int ContactId { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string TelLink { get; set; } = string.Empty;
        public string WhatsAppLink { get; set; } = string.Empty;
        public ListingType ListingType { get; set; }
        public PropertyType? PropertyType { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? RoomCount { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Notes { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public bool CanEdit { get; set; }

        public string Location => string.Join(" / ", new[] { District, City }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // "5.000.000 – 8.000.000 ₺", "en çok 8.000.000 ₺" ...
        public string? PriceText
        {
            get
            {
                string F(decimal v) => v.ToString("N0", PropertyLabels.Tr);
                var suffix = " ₺" + (ListingType == ListingType.Rent ? " / ay" : "");
                if (MinPrice != null && MaxPrice != null) return $"{F(MinPrice.Value)} – {F(MaxPrice.Value)}{suffix}";
                if (MaxPrice != null) return $"en çok {F(MaxPrice.Value)}{suffix}";
                if (MinPrice != null) return $"en az {F(MinPrice.Value)}{suffix}";
                return null;
            }
        }

        // Kısa özet: "Satılık · Konut · Kadıköy / İstanbul · 3+1"
        public string Summary => string.Join(" · ", new[]
        {
            PropertyLabels.Listing(ListingType),
            PropertyType == null ? null : PropertyLabels.Type(PropertyType.Value),
            Location.Length == 0 ? null : Location,
            RoomCount
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // "Uygun portföyler": portföy listesini talep ölçütleriyle açar (aktifler; arama ilçe ya da şehir)
        public Dictionary<string, string> MatchRoute
        {
            get
            {
                var route = new Dictionary<string, string>
                {
                    ["listingType"] = ListingType.ToString(),
                    ["status"] = PropertyStatus.Active.ToString()
                };
                if (PropertyType != null) route["propertyType"] = PropertyType.Value.ToString();
                if (MinPrice != null) route["minPrice"] = MinPrice.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                if (MaxPrice != null) route["maxPrice"] = MaxPrice.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                var place = !string.IsNullOrWhiteSpace(District) ? District : City;
                if (!string.IsNullOrWhiteSpace(place)) route["q"] = place!;
                return route;
            }
        }

        public static CustomerRequestItemViewModel From(CustomerRequest r, string contactName, string phoneNormalized,
            string createdByName, bool canEdit)
        {
            var phoneOk = phoneNormalized.Length == 10 && phoneNormalized.All(char.IsAsciiDigit);
            return new CustomerRequestItemViewModel
            {
                CustomerRequestId = r.CustomerRequestId,
                ContactId = r.ContactId,
                ContactName = contactName,
                TelLink = phoneOk ? $"tel:+90{phoneNormalized}" : string.Empty,
                WhatsAppLink = phoneOk ? $"https://wa.me/{BusinessLayer.Helpers.PhoneNormalizer.ToWhatsApp(phoneNormalized)}" : string.Empty,
                ListingType = r.ListingType,
                PropertyType = r.PropertyType,
                City = r.City,
                District = r.District,
                RoomCount = r.RoomCount,
                MinPrice = r.MinPrice,
                MaxPrice = r.MaxPrice,
                Notes = r.Notes,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                CreatedByName = createdByName,
                CanEdit = canEdit
            };
        }

        public static CustomerRequestItemViewModel From(CustomerRequestRow row, bool canEdit) =>
            From(row.Request, row.ContactName, row.ContactPhoneNormalized, row.CreatedByName, canEdit);
    }

    // Müşteri detayındaki "Talepleri" bölümü
    public class CustomerRequestPanelViewModel
    {
        public int ContactId { get; set; }
        public bool CanEdit { get; set; }
        public List<CustomerRequestItemViewModel> Items { get; set; } = new();
    }

    // "Talepler" sayfası (menü)
    public class CustomerRequestListViewModel
    {
        public List<CustomerRequestItemViewModel> Items { get; set; } = new();
        public string? Q { get; set; }
        public ListingType? ListingType { get; set; }
        public PropertyType? PropertyType { get; set; }
        public RequestStatus? Status { get; set; }

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    // Ekleme / düzenleme formu. Id, müşteri, oluşturan, tarih ve durum YOK (over-posting kapalı).
    // Fiyatlar sadece rakam (portföydeki gibi metin olarak alınır).
    public class CustomerRequestFormViewModel
    {
        [Display(Name = "Satılık / kiralık")]
        public ListingType ListingType { get; set; } = ListingType.Sale;

        [Display(Name = "Tür")]
        public PropertyType? PropertyType { get; set; }

        [StringLength(50, ErrorMessage = "Şehir en fazla 50 karakter olabilir.")]
        [Display(Name = "Şehir")]
        public string? City { get; set; }

        [StringLength(50, ErrorMessage = "İlçe en fazla 50 karakter olabilir.")]
        [Display(Name = "İlçe")]
        public string? District { get; set; }

        [StringLength(10, ErrorMessage = "Oda sayısı en fazla 10 karakter olabilir.")]
        [Display(Name = "Oda sayısı")]
        public string? RoomCount { get; set; }

        [Display(Name = "En az fiyat")]
        public string? MinPriceText { get; set; }

        [Display(Name = "En çok fiyat")]
        public string? MaxPriceText { get; set; }

        [StringLength(2000, ErrorMessage = "Not en fazla 2000 karakter olabilir.")]
        [Display(Name = "Not")]
        public string? Notes { get; set; }
    }
}
