using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Talep listesi filtreleri. Görünürlük (bağlı müşterinin görünürlüğü) bu filtrelerden AYRI ve her zaman uygulanır.
    public class CustomerRequestFilter
    {
        public string? Search { get; set; }              // şehir veya ilçe
        public ListingType? ListingType { get; set; }
        public PropertyType? PropertyType { get; set; }
        public RequestStatus? Status { get; set; }       // null: hepsi
        public int? ContactId { get; set; }              // müşteri detayındaki liste
    }

    // Liste satırı: talep + bağlı müşterinin adı/telefonu (müşteri zaten görünür olduğu için satır var)
    public class CustomerRequestRow
    {
        public CustomerRequest Request { get; set; } = null!;
        public string ContactName { get; set; } = string.Empty;
        public string ContactPhoneNormalized { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
    }
}
