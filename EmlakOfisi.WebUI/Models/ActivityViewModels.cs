using System.ComponentModel.DataAnnotations;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.WebUI.Models
{
    // Görüşme geçmişindeki bir satır
    public class ActivityItemViewModel
    {
        public int ActivityId { get; set; }
        public ActivityType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime ActivityDate { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool CanDelete { get; set; }

        public string TypeLabel => ActivityTypes.Label(Type);
        public string Icon => ActivityTypes.Icon(Type);
    }

    // Yeni görüşme formu. Müşteri id'si adresten, kaydı giren kişi oturumdan gelir (formda yok).
    public class ActivityFormViewModel
    {
        [Display(Name = "Tür")]
        public ActivityType Type { get; set; } = ActivityType.Call;

        [Required(ErrorMessage = "Görüşme notu zorunludur.")]
        [StringLength(2000, ErrorMessage = "Not en fazla 2000 karakter olabilir.")]
        [Display(Name = "Not")]
        public string Description { get; set; } = string.Empty;

        // Nullable: boş gönderilirse binder'ın İngilizce mesajı yerine [Required]'ın Türkçe mesajı çıksın
        [Required(ErrorMessage = "Tarih zorunludur.")]
        [Display(Name = "Tarih")]
        public DateTime? ActivityDate { get; set; } = DateTime.Now;
    }

    // Tür adları ve ikonları tek yerde
    public static class ActivityTypes
    {
        public static readonly ActivityType[] All =
        {
            ActivityType.Call, ActivityType.WhatsApp, ActivityType.Meeting,
            ActivityType.Showing, ActivityType.Offer, ActivityType.Note
        };

        public static string Label(ActivityType type) => type switch
        {
            ActivityType.Call => "Telefon",
            ActivityType.WhatsApp => "WhatsApp",
            ActivityType.Meeting => "Yüz yüze görüşme",
            ActivityType.Showing => "Gösterim",
            ActivityType.Offer => "Teklif / pazarlık",
            ActivityType.Note => "Not",
            _ => type.ToString()
        };

        public static string Icon(ActivityType type) => type switch
        {
            ActivityType.Call => "ti-phone",
            ActivityType.WhatsApp => "ti-brand-whatsapp",
            ActivityType.Meeting => "ti-users",
            ActivityType.Showing => "ti-home-search",
            ActivityType.Offer => "ti-cash",
            _ => "ti-note"
        };
    }
}
