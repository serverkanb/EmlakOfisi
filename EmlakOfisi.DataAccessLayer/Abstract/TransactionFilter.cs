using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // İşlem listesi filtreleri. Görünürlük bu filtrelerden AYRI ve her zaman uygulanır.
    public class TransactionFilter
    {
        public TransactionScope? Scope { get; set; }   // null: hepsi
        public bool OnlyMine { get; set; }             // sadece sorumlusu olduğum
        public int? PropertyId { get; set; }           // portföy detayındaki liste
        public int? ContactId { get; set; }            // müşteri detayındaki liste
    }

    // Liste/okuma satırı. Contact ve Property sadece GÖRÜNTÜLEYEN kullanıcıya görünüyorsa dolu;
    // null ise kayıt gizlidir (ad, telefon, id gösterilmez).
    public class TransactionRow
    {
        public Transaction Transaction { get; set; } = null!;
        public string AgentName { get; set; } = string.Empty;
        public Contact? Contact { get; set; }
        public Property? Property { get; set; }

        public bool ContactHidden => Contact == null;
        public bool PropertyHidden => Property == null;
    }

    // Ana ekran: tarih aralığında tamamlanan işlemler (satış/kiralama sayısı ve komisyon toplamı)
    public class TransactionSummary
    {
        public int SaleCount { get; set; }
        public int RentCount { get; set; }
        public decimal Commission { get; set; }
        public decimal AgentShare { get; set; }      // görünür işlemlerin prim toplamı (yönetici: ödenecek primler)
        public decimal MyAgentShare { get; set; }    // sorumlusu kullanıcı olan işlemlerin primi (danışman: primim)
    }
}
