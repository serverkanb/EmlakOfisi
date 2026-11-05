using EmlakOfisi.EntityLayer.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Satış / kiralama işlemi. Pazarlık adımları Activity (Type = Offer) olarak tutulur.
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

        public int AgentUserId { get; set; }
        public AppUser? AgentUser { get; set; }

        public DateTime? CompletedAt { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
