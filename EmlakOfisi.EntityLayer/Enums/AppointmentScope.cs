namespace EmlakOfisi.EntityLayer.Enums
{
    // Randevu listesi sekmeleri (veritabanında saklanmaz; liste filtresi ve adres parametresi)
    public enum AppointmentScope
    {
        Upcoming = 1,   // Planlandı ve bugün ya da sonrası (en yakın üstte)
        Past = 2,       // Yapıldı ya da tarihi geçmiş (iptaller hariç; en yeni üstte)
        Cancelled = 3   // İptal edilenler
    }
}
