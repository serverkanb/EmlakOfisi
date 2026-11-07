using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Randevu listesi filtreleri. Görünürlük bu filtrelerden AYRI ve her zaman uygulanır.
    public class AppointmentFilter
    {
        public AppointmentScope? Scope { get; set; }   // null: hepsi
        public bool OnlyMine { get; set; }             // sadece sorumlusu olduğum
        public DateTime Today { get; set; } = DateTime.Today;
    }

    // Liste/okuma satırı. Contact ve Property sadece GÖRÜNTÜLEYEN kullanıcıya görünüyorsa dolu;
    // randevu bağlı olduğu halde null ise kayıt gizlidir (ad, telefon, id verilmez).
    public class AppointmentRow
    {
        public Appointment Appointment { get; set; } = null!;
        public string AgentName { get; set; } = string.Empty;
        public Contact? Contact { get; set; }
        public Property? Property { get; set; }

        public bool ContactHidden => Appointment.ContactId != null && Contact == null;
        public bool PropertyHidden => Appointment.PropertyId != null && Property == null;
    }
}
