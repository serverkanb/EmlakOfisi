using EmlakOfisi.EntityLayer.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Müşteri talebi (aradığı gayrimenkul). Görünürlüğü bağlı olduğu Contact'tan gelir.
    public class CustomerRequest
    {
        public int CustomerRequestId { get; set; }

        public int ContactId { get; set; }
        public Contact? Contact { get; set; }

        public ListingType ListingType { get; set; } = ListingType.Sale;
        public PropertyType? PropertyType { get; set; }

        [StringLength(50)]
        public string? City { get; set; }

        [StringLength(50)]
        public string? District { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxPrice { get; set; }

        [StringLength(10)]
        public string? RoomCount { get; set; }

        // En az alan (m²). Arsada oda sayısı yerine kullanılır (1 dönüm = 1.000 m²); diğer türlerde isteğe bağlı.
        public int? MinArea { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public RequestStatus Status { get; set; } = RequestStatus.Open;

        public int CreatedByUserId { get; set; }
        public AppUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
