namespace EmlakOfisi.EntityLayer.Enums
{
    // İşlem listesi sekmeleri (veritabanında saklanmaz; liste filtresi ve adres parametresi)
    public enum TransactionScope
    {
        Open = 1,        // Teklif, Pazarlık, Anlaşıldı
        Completed = 2,   // Tamamlandı
        Cancelled = 3    // İptal
    }
}
