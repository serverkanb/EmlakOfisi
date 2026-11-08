using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Satış / kiralama işlemi. Pazarlık adımları Activity (Type = Offer) olarak tutulur.
    // Görünürlük (görev 18): yönetici, sorumlu danışman ya da işlemi oluşturan (komisyon içerdiği için).
    [Index(nameof(AgentUserId), nameof(Status))]
    [Index(nameof(CreatedByUserId))]
    [Index(nameof(Type), nameof(Status), nameof(LeaseEndDate))]   // biten kira kontratları (görev 23)
    public class Transaction
    {
        public int TransactionId { get; set; }

        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        public int CustomerContactId { get; set; }   // Alıcı / kiracı
        public Contact? CustomerContact { get; set; }

        public ListingType Type { get; set; } = ListingType.Sale;
        public TransactionStatus Status { get; set; } = TransactionStatus.Offer;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OfferPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AgreedPrice { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? CommissionRate { get; set; }   // Yüzde, örn: 2.00

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CommissionAmount { get; set; }

        // Danışman primi (görev 18b): tamamlanınca danışmanın o anki oranı kopyalanır (yönetici elle değiştirebilir);
        // tutar = komisyon x oran / 100. Tamamlanmamış işlemde tutar boş.
        [Column(TypeName = "decimal(5,2)")]
        public decimal? AgentShareRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AgentShareAmount { get; set; }

        public int AgentUserId { get; set; }
        public AppUser? AgentUser { get; set; }

        public DateTime? CompletedAt { get; set; }

        // Tamamlandı ya da İptal olduğu an (görev 18). Danışman kapanmış işlemi sadece bu gün içinde değiştirebilir.
        public DateTime? ClosedAt { get; set; }

        public int CreatedByUserId { get; set; }
        public AppUser? CreatedByUser { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        // Kira tarihleri (görev 22): sadece kiralamada; satışta sunucu boşaltır.
        public DateTime? LeaseStartDate { get; set; }
        public DateTime? LeaseEndDate { get; set; }

        // Kontrat dosyası (görev 22): işlem başına tek dosya, wwwroot DIŞINDA ("Documents:Root").
        // Dosya adı sunucuda üretilir ("{işlemId}/{guid}{uzantı}"); kullanıcının dosya adı sadece gösterim için saklanır.
        [StringLength(260)]
        public string? ContractFileName { get; set; }

        [StringLength(260)]
        public string? ContractOriginalName { get; set; }

        [StringLength(50)]
        public string? ContractContentType { get; set; }

        public DateTime? ContractUploadedAt { get; set; }

        public int? ContractUploadedByUserId { get; set; }
        public AppUser? ContractUploadedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
