using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.WebUI.Models
{
    // Portföyle ilgilenen müşteri satırı (görev 12c). Entity view'a verilmez; sadece gösterilecek alanlar.
    public class InterestItemViewModel
    {
        public int PropertyInterestId { get; set; }

        // Müşteri tarafı (portföy sayfasında gösterilir)
        public int ContactId { get; set; }
        public string ContactName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string TelLink { get; set; } = string.Empty;
        public string WhatsAppLink { get; set; } = string.Empty;

        // Portföy tarafı (müşteri sayfasında gösterilir)
        public int PropertyId { get; set; }
        public string PropertyTitle { get; set; } = string.Empty;
        public string PropertyPriceText { get; set; } = string.Empty;

        public string? Note { get; set; }
        public string AddedBy { get; set; } = string.Empty;   // sadece ad soyad
        public DateTime CreatedAt { get; set; }
        public bool CanDelete { get; set; }

        // Linkler sadece normalize edilmiş 10 haneden kurulur; geçersiz numarada buton yok
        public static InterestItemViewModel From(PropertyInterest i, bool canDelete)
        {
            var c = i.Contact;
            var p = i.Property;
            var phoneOk = c != null && c.PhoneNormalized.Length == 10 && c.PhoneNormalized.All(char.IsAsciiDigit);

            return new InterestItemViewModel
            {
                PropertyInterestId = i.PropertyInterestId,
                ContactId = i.ContactId,
                ContactName = c == null ? "" : $"{c.Name} {c.Surname}",
                Phone = c?.Phone,
                TelLink = phoneOk ? $"tel:+90{c!.PhoneNormalized}" : string.Empty,
                WhatsAppLink = phoneOk ? $"https://wa.me/{PhoneNormalizer.ToWhatsApp(c!.PhoneNormalized)}" : string.Empty,
                PropertyId = i.PropertyId,
                PropertyTitle = p?.Title ?? "",
                PropertyPriceText = p == null ? "" : PropertyLabels.Price(p.Price, p.ListingType),
                Note = i.Note,
                AddedBy = i.CreatedByUser == null ? "" : $"{i.CreatedByUser.Name} {i.CreatedByUser.Surname}",
                CreatedAt = i.CreatedAt,
                CanDelete = canDelete
            };
        }
    }

    // Portföy detayında "İlgilenenler", müşteri detayında "İlgilendiği portföyler" kartı
    public class InterestPanelViewModel
    {
        public bool ForContact { get; set; }    // true: müşteri sayfası (satırlar portföy), false: portföy sayfası
        public int AnchorId { get; set; }       // sayfanın kaydı (portföy ya da müşteri id'si)
        public List<InterestItemViewModel> Items { get; set; } = new();

        // Ekleme seçenekleri: sadece kullanıcının görebildiği ve listede olmayan kayıtlar
        public List<(int Id, string Name)> Choices { get; set; } = new();
        public string ChoiceHint { get; set; } = string.Empty;
    }
}
