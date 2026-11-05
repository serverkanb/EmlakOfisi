using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Müşteri / kişi. Bir kişi birden fazla role sahip olabilir (IsBuyer, IsSeller...), kayıt tek kalır.
    [Index(nameof(PhoneNormalized))]
    [Index(nameof(CreatedByUserId), nameof(Visibility))]
    [Index(nameof(Surname), nameof(Name))]
    public class Contact
    {
        public int ContactId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        // Telefonun son 10 hanesi (boşluk, 0 ve 90 öneki atılmış). Arama ve tekrar kayıt kontrolü için.
        [Required]
        [StringLength(15)]
        public string PhoneNormalized { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Phone2 { get; set; }

        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        // Roller
        public bool IsBuyer { get; set; }     // Alıcı
        public bool IsSeller { get; set; }    // Satıcı
        public bool IsTenant { get; set; }    // Kiracı
        public bool IsLandlord { get; set; }  // Mülk sahibi (kiraya veren)

        // Görünürlük ve sahiplik
        public Visibility Visibility { get; set; } = Visibility.OnlyMe;

        public int CreatedByUserId { get; set; }
        public AppUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        public List<Activity> Activities { get; set; } = new();
        public List<CustomerRequest> CustomerRequests { get; set; } = new();
    }
}
