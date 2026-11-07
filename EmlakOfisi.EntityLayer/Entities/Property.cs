using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Portföy (satılık / kiralık gayrimenkul)
    [Index(nameof(Status), nameof(ListingType))]
    [Index(nameof(AgentUserId))]
    [Index(nameof(CreatedByUserId), nameof(Visibility))]
    [Index(nameof(City), nameof(District))]
    public class Property
    {
        public int PropertyId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public ListingType ListingType { get; set; } = ListingType.Sale;
        public PropertyType PropertyType { get; set; } = PropertyType.Residence;
        public PropertyStatus Status { get; set; } = PropertyStatus.Active;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        // Adres ve konum
        [Required]
        [StringLength(50)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string District { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Neighborhood { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal? Latitude { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal? Longitude { get; set; }

        // Özellikler
        public int? GrossArea { get; set; }   // Brüt m²
        public int? NetArea { get; set; }     // Net m²

        [StringLength(10)]
        public string? RoomCount { get; set; } // Örn: "3+1"

        public int? Floor { get; set; }
        public int? BuildingAge { get; set; }

        [StringLength(4000)]
        public string? Description { get; set; }

        // Dış ilan sitesindeki ilanın adresi (sahibinden.com vb.). Sadece http/https kabul edilir.
        [StringLength(500)]
        public string? ListingUrl { get; set; }

        // İlişkiler
        public int? OwnerContactId { get; set; }   // Mülk sahibi
        public Contact? OwnerContact { get; set; }

        public int AgentUserId { get; set; }       // Sorumlu danışman
        public AppUser? AgentUser { get; set; }

        // Görünürlük ve sahiplik
        public Visibility Visibility { get; set; } = Visibility.OnlyMe;

        public int CreatedByUserId { get; set; }
        public AppUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public List<PropertyPhoto> Photos { get; set; } = new();
    }
}
