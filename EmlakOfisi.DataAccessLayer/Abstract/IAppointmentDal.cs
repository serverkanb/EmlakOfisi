using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Randevu veri erişimi. Görünürlük kuralı (VisibilityRules) sorguların İÇİNDE uygulanır.
    // Generic yapıdan TÜREMEZ: filtresiz liste/okuma ve gerçek silme yoktur (iptal = durum).
    public interface IAppointmentDal
    {
        // Bağlı müşteri/portföy aynı sorguda, görüntüleyenin görünürlüğüyle süzülerek gelir
        List<AppointmentRow> GetVisibleList(int userId, bool canSeeAll, AppointmentFilter filter, int skip, int take);

        int CountVisible(int userId, bool canSeeAll, AppointmentFilter filter);

        // Görünmüyorsa null. Salt okunur.
        AppointmentRow? GetVisibleById(int id, int userId, bool canSeeAll);

        // Aynı kural, EF takip eder: sadece servis içindeki güncelleme akışında kullanılır
        Appointment? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll);

        void Create(Appointment appointment);

        void Update(Appointment appointment);
    }
}
