using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Randevu: müşteri görüşmesi veya gayrimenkul gösterimi
    [Index(nameof(AgentUserId), nameof(StartAt))]
    public class Appointment
    {
        public int AppointmentId { get; set; }

        public AppointmentType Type { get; set; } = AppointmentType.Meeting;

        public DateTime StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public int? ContactId { get; set; }
        public Contact? Contact { get; set; }

        public int? PropertyId { get; set; }
        public Property? Property { get; set; }

        public int AgentUserId { get; set; }
        public AppUser? AgentUser { get; set; }

        [StringLength(300)]
        public string? Location { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Planned;

        public int CreatedByUserId { get; set; }
        public AppUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
