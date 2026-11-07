using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Ofis çalışanı (Admin, Manager, Agent). InstrumentTrade'deki AppUser ile aynı yapı.
    public class AppUser : IdentityUser<int>
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Surname { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Danışman primi (görev 18b): ofisin aldığı komisyondan danışmana düşen yüzde (ör. 40). Sadece Admin yazar.
        // Boşsa prim hesaplanmaz. İşlem tamamlanınca o anki oran işleme kopyalanır.
        [Column(TypeName = "decimal(5,2)")]
        public decimal? CommissionSharePercent { get; set; }
    }
}
