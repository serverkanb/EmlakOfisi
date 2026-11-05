using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Görüşme ve işlem geçmişi kaydı. Görünürlüğü bağlı olduğu Contact'tan gelir.
    [Index(nameof(ContactId), nameof(ActivityDate))]
    public class Activity
    {
        public int ActivityId { get; set; }

        public int ContactId { get; set; }
        public Contact? Contact { get; set; }

        public int? PropertyId { get; set; }
        public Property? Property { get; set; }

        public ActivityType Type { get; set; } = ActivityType.Call;

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        public DateTime ActivityDate { get; set; } = DateTime.Now;

        public int UserId { get; set; }   // Kaydı giren çalışan
        public AppUser? User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
