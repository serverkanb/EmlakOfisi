using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Concrete
{
    public class AppointmentManager : IAppointmentService
    {
        private readonly IAppointmentDal _appointmentDal;
        private readonly IContactService _contactService;
        private readonly IPropertyService _propertyService;

        // Müşteri/portföy görünürlüğü ve atanabilir çalışan kuralı tek yerde kalsın diye ilgili servisler kullanılır
        public AppointmentManager(IAppointmentDal appointmentDal, IContactService contactService, IPropertyService propertyService)
        {
            _appointmentDal = appointmentDal;
            _contactService = contactService;
            _propertyService = propertyService;
        }

        public (List<AppointmentRow> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            AppointmentFilter filter, int page, int pageSize)
        {
            page = Math.Clamp(page, 1, 100_000);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var items = _appointmentDal.GetVisibleList(userId, canSeeAll, filter, (page - 1) * pageSize, pageSize);
            var total = _appointmentDal.CountVisible(userId, canSeeAll, filter);
            return (items, total);
        }

        public AppointmentRow? TGetVisibleById(int id, int userId, bool canSeeAll)
        {
            return _appointmentDal.GetVisibleById(id, userId, canSeeAll);
        }

        // Görünürlükten AYRI bir kural (CLAUDE.md kural 3). Bugün kullanıcı kararıyla aynı koşul ("ajandayı gören düzenler");
        // görünürlük ileride genişletilirse (ör. portföyü gören gösterimini görür) bu kural kendiliğinden genişlemez.
        public bool TCanEdit(Appointment appointment, int userId, bool canSeeAll)
        {
            return canSeeAll || appointment.AgentUserId == userId || appointment.CreatedByUserId == userId;
        }

        public OperationResult TCreate(Appointment appointment, int userId, bool canSeeAll)
        {
            if (!DatesValid(appointment.StartAt, appointment.EndAt))
                return OperationResult.Invalid;
            if (appointment.ContactId != null && !ContactVisible(appointment.ContactId.Value, userId, canSeeAll))
                return OperationResult.Invalid;
            if (appointment.PropertyId != null && !PropertyVisible(appointment.PropertyId.Value, userId, canSeeAll))
                return OperationResult.Invalid;

            // Danışman sadece kendi adına ekler; yönetici başka bir çalışanı seçebilir
            if (!canSeeAll || appointment.AgentUserId == 0)
                appointment.AgentUserId = userId;
            else if (appointment.AgentUserId != userId && !IsAssignable(appointment.AgentUserId))
                return OperationResult.Invalid;

            appointment.AppointmentId = 0;
            appointment.CreatedByUserId = userId;
            appointment.CreatedAt = DateTime.Now;
            appointment.Status = AppointmentStatus.Planned;
            if (!Enum.IsDefined(appointment.Type))
                appointment.Type = AppointmentType.Meeting;
            appointment.Contact = null;
            appointment.Property = null;
            appointment.AgentUser = null;
            appointment.CreatedByUser = null;

            _appointmentDal.Create(appointment);
            return OperationResult.Success;
        }

        public OperationResult TUpdate(int id, Appointment changes, int userId, bool canSeeAll)
        {
            var appointment = _appointmentDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (appointment == null)
                return OperationResult.NotFound;
            if (!TCanEdit(appointment, userId, canSeeAll))
                return OperationResult.Forbidden;

            if (!DatesValid(changes.StartAt, changes.EndAt))
                return OperationResult.Invalid;

            // Bağlı kayıt: mevcudu göremeyen değiştiremez (korunur); yeni değer görünür olmalı
            if (!ResolveLink(appointment.ContactId, changes.ContactId, id => ContactVisible(id, userId, canSeeAll), out var contactId))
                return OperationResult.Invalid;
            if (!ResolveLink(appointment.PropertyId, changes.PropertyId, id => PropertyVisible(id, userId, canSeeAll), out var propertyId))
                return OperationResult.Invalid;

            // Sorumlu sadece yönetici tarafından ve aktif bir çalışana değişir; aynı değer geri gelirse dokunulmaz
            var agentId = appointment.AgentUserId;
            if (canSeeAll && changes.AgentUserId != 0 && changes.AgentUserId != appointment.AgentUserId)
            {
                if (!IsAssignable(changes.AgentUserId))
                    return OperationResult.Invalid;
                agentId = changes.AgentUserId;
            }

            appointment.Type = Enum.IsDefined(changes.Type) ? changes.Type : appointment.Type;
            appointment.Status = Enum.IsDefined(changes.Status) ? changes.Status : appointment.Status;
            appointment.StartAt = changes.StartAt;
            appointment.EndAt = changes.EndAt;
            appointment.ContactId = contactId;
            appointment.PropertyId = propertyId;
            appointment.AgentUserId = agentId;
            appointment.Location = changes.Location;
            appointment.Notes = changes.Notes;

            _appointmentDal.Update(appointment);
            return OperationResult.Success;
        }

        public OperationResult TSetStatus(int id, AppointmentStatus status, int userId, bool canSeeAll)
        {
            var appointment = _appointmentDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (appointment == null)
                return OperationResult.NotFound;
            if (!TCanEdit(appointment, userId, canSeeAll))
                return OperationResult.Forbidden;
            if (!Enum.IsDefined(status))
                return OperationResult.Invalid;

            appointment.Status = status;
            _appointmentDal.Update(appointment);
            return OperationResult.Success;
        }

        // ---------------- Yardımcılar ----------------

        private static bool DatesValid(DateTime start, DateTime? end) =>
            start != default && (end == null || end > start);

        private bool ContactVisible(int id, int userId, bool canSeeAll) =>
            _contactService.TGetVisibleById(id, userId, canSeeAll) != null;

        private bool PropertyVisible(int id, int userId, bool canSeeAll) =>
            _propertyService.TGetVisibleById(id, userId, canSeeAll) != null;

        private bool IsAssignable(int agentId) =>
            _propertyService.TGetAssignableAgents().Any(a => a.Id == agentId);

        // Mevcut bağlantı kullanıcıya görünmüyorsa korunur; değilse yeni değer (null = kaldır) görünür olmalı
        private static bool ResolveLink(int? current, int? requested, Func<int, bool> isVisible, out int? result)
        {
            result = current;
            if (current != null && !isVisible(current.Value))
                return true;
            if (requested == current)
                return true;
            if (requested != null && !isVisible(requested.Value))
                return false;
            result = requested;
            return true;
        }
    }
}
