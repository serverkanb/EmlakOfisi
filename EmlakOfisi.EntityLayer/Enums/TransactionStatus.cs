namespace EmlakOfisi.EntityLayer.Enums
{
    // Satış / kiralama işlemi durumu
    public enum TransactionStatus
    {
        Offer = 1,       // Teklif
        Negotiation = 2, // Pazarlık
        Agreed = 3,      // Anlaşıldı
        Completed = 4,   // Tamamlandı
        Cancelled = 5    // İptal
    }
}
