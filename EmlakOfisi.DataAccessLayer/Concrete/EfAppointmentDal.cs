using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    public class EfAppointmentDal : IAppointmentDal
    {
        private readonly AppDbContext _context;

        public EfAppointmentDal(AppDbContext context)
        {
            _context = context;
        }

        private IQueryable<Appointment> Visible(int userId, bool canSeeAll) =>
            _context.Appointments.VisibleTo(userId, canSeeAll);

        private IQueryable<Appointment> Filtered(int userId, bool canSeeAll, AppointmentFilter f)
        {
            var query = Visible(userId, canSeeAll);

            query = f.Scope switch
            {
                AppointmentScope.Upcoming => query.Where(a => a.Status == AppointmentStatus.Planned && a.StartAt >= f.Today),
                AppointmentScope.Past => query.Where(a => a.Status != AppointmentStatus.Cancelled &&
                                                          (a.Status == AppointmentStatus.Done || a.StartAt < f.Today)),
                AppointmentScope.Cancelled => query.Where(a => a.Status == AppointmentStatus.Cancelled),
                _ => query
            };

            if (f.OnlyMine) query = query.Where(a => a.AgentUserId == userId);
            return query;
        }

        // Bağlı kayıtlar, görüntüleyen kullanıcının görünürlük kuralıyla aynı sorguda okunur (N+1 yok).
        // Appointment.Contact / Property navigation'ları YÜKLENMEZ; sadece görünür olan kopya satıra konur.
        private IQueryable<AppointmentRow> ToRows(IQueryable<Appointment> query, int userId, bool canSeeAll)
        {
            var contacts = _context.Contacts.VisibleTo(userId, canSeeAll);
            var properties = _context.Properties.VisibleTo(userId, canSeeAll);

            return query.Select(a => new AppointmentRow
            {
                Appointment = a,
                AgentName = a.AgentUser!.Name + " " + a.AgentUser.Surname,
                Contact = contacts.FirstOrDefault(c => c.ContactId == a.ContactId),
                Property = properties.FirstOrDefault(p => p.PropertyId == a.PropertyId)
            });
        }

        public List<AppointmentRow> GetVisibleList(int userId, bool canSeeAll, AppointmentFilter filter, int skip, int take)
        {
            var query = Filtered(userId, canSeeAll, filter).AsNoTracking();

            // Yaklaşanlarda en yakın üstte, diğerlerinde en yeni üstte
            query = filter.Scope == AppointmentScope.Upcoming
                ? query.OrderBy(a => a.StartAt).ThenBy(a => a.AppointmentId)
                : query.OrderByDescending(a => a.StartAt).ThenByDescending(a => a.AppointmentId);

            return ToRows(query.Skip(skip).Take(take), userId, canSeeAll).ToList();
        }

        public int CountVisible(int userId, bool canSeeAll, AppointmentFilter filter)
        {
            return Filtered(userId, canSeeAll, filter).Count();
        }

        public AppointmentRow? GetVisibleById(int id, int userId, bool canSeeAll)
        {
            return ToRows(Visible(userId, canSeeAll).AsNoTracking().Where(a => a.AppointmentId == id), userId, canSeeAll)
                .FirstOrDefault();
        }

        public Appointment? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll).FirstOrDefault(a => a.AppointmentId == id);
        }

        public void Create(Appointment appointment)
        {
            _context.Appointments.Add(appointment);
            _context.SaveChanges();
        }

        public void Update(Appointment appointment)
        {
            if (_context.Entry(appointment).State == EntityState.Detached)
                throw new InvalidOperationException("Güncellenecek randevu önce GetVisibleByIdForUpdate ile okunmalıdır.");

            _context.SaveChanges();
        }
    }
}
