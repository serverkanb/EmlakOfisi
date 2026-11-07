using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Bir portföyle ilgilenen müşteri ("şu kişiler bakıyor"). Aynı kişi aynı portföye bir kez eklenir.
    // Kendi görünürlüğü yoktur: satırı görmek için hem portföyü hem müşteriyi görebiliyor olmak gerekir.
    [Index(nameof(PropertyId), nameof(ContactId), IsUnique = true)]
    [Index(nameof(ContactId))]
    public class PropertyInterest
    {
        public int PropertyInterestId { get; set; }

        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        public int ContactId { get; set; }
        public Contact? Contact { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }      // Örn: "Hafta sonu görmek istiyor"

        public int CreatedByUserId { get; set; }  // Ekleyen çalışan
        public AppUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
