using System.ComponentModel.DataAnnotations;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.WebUI.Models
{
    // Randevu kartı. Entity view'a verilmez. Bağlı müşteri/portföy görüntüleyene görünmüyorsa
    // ad, telefon ve id taşınmaz; sadece "gizli" bayrağı gelir.
    public class AppointmentItemViewModel
    {
        public int AppointmentId { get; set; }
        public AppointmentType Type { get; set; }
        public AppointmentStatus Status { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public bool IsMine { get; set; }
        public string? Location { get; set; }
        public string? Notes { get; set; }

        public int? ContactId { get; set; }
        public string? ContactName { get; set; }
        public bool ContactHidden { get; set; }
        public string TelLink { get; set; } = string.Empty;
        public string WhatsAppLink { get; set; } = string.Empty;

        public int? PropertyId { get; set; }
        public string? PropertyTitle { get; set; }
        public bool PropertyHidden { get; set; }
        public string? MapLink { get; set; }

        public bool CanEdit { get; set; }

        // "Müşteri · Portföy" kısa özeti (gizli kayıtların sadece "Gizli kayıt" yazısıyla)
        public string LinkSummary => string.Join(" · ", new[]
        {
            ContactName ?? (ContactHidden ? "Gizli kayıt" : null),
            PropertyTitle ?? (PropertyHidden ? "Gizli kayıt" : null)
        }.Where(s => s != null));

        public string TimeText => EndAt == null
            ? StartAt.ToString("HH:mm")
            : $"{StartAt:HH:mm}–{(EndAt.Value.Date == StartAt.Date ? EndAt.Value.ToString("HH:mm") : EndAt.Value.ToString("dd.MM HH:mm"))}";

        public static AppointmentItemViewModel From(AppointmentRow row, int userId, bool canEdit)
        {
            var a = row.Appointment;
            var c = row.Contact;
            var p = row.Property;
            var phoneOk = c != null && c.PhoneNormalized.Length == 10 && c.PhoneNormalized.All(char.IsAsciiDigit);

            return new AppointmentItemViewModel
            {
                AppointmentId = a.AppointmentId,
                Type = a.Type,
                Status = a.Status,
                StartAt = a.StartAt,
                EndAt = a.EndAt,
                AgentName = row.AgentName,
                IsMine = a.AgentUserId == userId,
                Location = a.Location,
                Notes = a.Notes,
                ContactId = c?.ContactId,
                ContactName = c == null ? null : $"{c.Name} {c.Surname}",
                ContactHidden = row.ContactHidden,
                TelLink = phoneOk ? $"tel:+90{c!.PhoneNormalized}" : string.Empty,
                WhatsAppLink = phoneOk ? $"https://wa.me/{PhoneNormalizer.ToWhatsApp(c!.PhoneNormalized)}" : string.Empty,
                PropertyId = p?.PropertyId,
                PropertyTitle = p?.Title,
                PropertyHidden = row.PropertyHidden,
                MapLink = p == null ? null
                    : PropertyLabels.MapLink(p.Latitude, p.Longitude, p.Address, p.Neighborhood, p.District, p.City),
                CanEdit = canEdit
            };
        }
    }

    public class AppointmentListViewModel
    {
        public List<AppointmentItemViewModel> Items { get; set; } = new();
        public AppointmentScope Scope { get; set; } = AppointmentScope.Upcoming;
        public bool Mine { get; set; }
        public bool CanSeeAll { get; set; }

        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    // Ekleme / düzenleme formu. Id, oluşturan ve kayıt tarihi YOK (over-posting kapalı).
    // Durum sadece düzenlemede kullanılır; eklemede servis her zaman "Planlandı" yapar.
    public class AppointmentFormViewModel
    {
        [Display(Name = "Tür")]
        public AppointmentType Type { get; set; } = AppointmentType.Meeting;

        [Required(ErrorMessage = "Başlangıç tarihi ve saati zorunludur.")]
        [Display(Name = "Başlangıç")]
        public DateTime? StartAt { get; set; }

        [Display(Name = "Bitiş")]
        public DateTime? EndAt { get; set; }

        [Display(Name = "Müşteri")]
        public int? ContactId { get; set; }

        [Display(Name = "Portföy")]
        public int? PropertyId { get; set; }

        [Display(Name = "Sorumlu danışman")]
        public int AgentUserId { get; set; }

        [StringLength(300, ErrorMessage = "Yer en fazla 300 karakter olabilir.")]
        [Display(Name = "Yer")]
        public string? Location { get; set; }

        [StringLength(2000, ErrorMessage = "Not en fazla 2000 karakter olabilir.")]
        [Display(Name = "Not")]
        public string? Notes { get; set; }

        [Display(Name = "Durum")]
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Planned;
    }

    public class AppointmentFormOptions
    {
        public int? AppointmentId { get; set; }                               // düzenlemede
        public List<(int Id, string Name)> Contacts { get; set; } = new();    // kullanıcının görebildiği kişiler
        public List<(int Id, string Name)> Properties { get; set; } = new();  // görebildiği aktif portföyler
        public bool ContactHidden { get; set; }                               // mevcut bağlantı görünmüyor: değişmez
        public bool PropertyHidden { get; set; }
        public bool CanChooseAgent { get; set; }
        public List<(int Id, string Name)> Agents { get; set; } = new();
    }

    public static class AppointmentLabels
    {
        public static string Type(AppointmentType t) => t == AppointmentType.Showing ? "Gösterim" : "Görüşme";

        public static string Status(AppointmentStatus s) => s switch
        {
            AppointmentStatus.Done => "Yapıldı",
            AppointmentStatus.Cancelled => "İptal",
            _ => "Planlandı"
        };

        public static string DayTitle(DateTime day)
        {
            var today = DateTime.Today;
            var text = day.ToString("d MMMM yyyy, dddd", PropertyLabels.Tr);
            if (day == today) return "Bugün · " + text;
            if (day == today.AddDays(1)) return "Yarın · " + text;
            return text;
        }
    }
}
