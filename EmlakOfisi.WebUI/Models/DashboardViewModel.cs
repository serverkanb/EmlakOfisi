namespace EmlakOfisi.WebUI.Models
{
    // Ana ekran sayıları. Hepsi kullanıcının GÖREBİLDİĞİ kayıtlardan sayılır (görünürlük filtreli sorgular).
    public class DashboardViewModel
    {
        public int ActiveProperties { get; set; }
        public int ActiveSale { get; set; }
        public int ActiveRent { get; set; }
        public int PassiveProperties { get; set; }
        public int SoldProperties { get; set; }
        public int RentedProperties { get; set; }
        public int Contacts { get; set; }
        public bool CanSeeAll { get; set; }
    }
}
