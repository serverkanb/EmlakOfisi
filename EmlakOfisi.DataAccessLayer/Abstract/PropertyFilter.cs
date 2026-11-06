using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Portföy listesi filtreleri (hepsi isteğe bağlı). Görünürlük bu filtrelerden AYRI ve her zaman uygulanır.
    public class PropertyFilter
    {
        public string? Search { get; set; }              // başlık, şehir, ilçe, mahalle
        public ListingType? ListingType { get; set; }    // satılık / kiralık
        public PropertyType? PropertyType { get; set; }  // konut / arsa / iş yeri
        public PropertyStatus? Status { get; set; }      // aktif / pasif / satıldı / kiralandı
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool OnlyMine { get; set; }               // sadece oluşturduğum veya sorumlusu olduğum
    }
}
