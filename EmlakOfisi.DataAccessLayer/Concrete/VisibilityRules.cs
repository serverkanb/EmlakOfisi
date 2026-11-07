using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    // GÖRÜNÜRLÜK KURALLARI (tek yer). EfContactDal, EfPropertyDal ve iki kaydı birbirine bağlayan
    // sorgular (ör. portföyle ilgilenen müşteriler) hep bunları kullanır; kural başka yerde tekrar yazılmaz.
    public static class VisibilityRules
    {
        // Müşteri: silinmemiş VE (yönetici VEYA herkese açık VEYA kaydı oluşturan kişi)
        public static IQueryable<Contact> VisibleTo(this IQueryable<Contact> contacts, int userId, bool canSeeAll) =>
            contacts.Where(c => !c.IsDeleted &&
                (canSeeAll || c.Visibility == Visibility.Public || c.CreatedByUserId == userId));

        // Portföy: silinmemiş VE (yönetici VEYA herkese açık VEYA kaydı oluşturan VEYA sorumlu danışman)
        public static IQueryable<Property> VisibleTo(this IQueryable<Property> properties, int userId, bool canSeeAll) =>
            properties.Where(p => !p.IsDeleted &&
                (canSeeAll || p.Visibility == Visibility.Public ||
                 p.CreatedByUserId == userId || p.AgentUserId == userId));

        // Randevu (danışmanın ajandası): yönetici VEYA sorumlu danışman VEYA randevuyu oluşturan.
        // Bağlı müşteri/portföy ayrıca kendi kuralıyla kontrol edilir; görünmüyorsa bilgisi verilmez.
        public static IQueryable<Appointment> VisibleTo(this IQueryable<Appointment> appointments, int userId, bool canSeeAll) =>
            appointments.Where(a => canSeeAll || a.AgentUserId == userId || a.CreatedByUserId == userId);

        // Satış/kiralama işlemi (komisyon içerir): yönetici VEYA sorumlu danışman VEYA işlemi oluşturan.
        // Bağlı portföy/müşteri ayrıca kendi kuralıyla kontrol edilir; görünmüyorsa bilgisi verilmez.
        public static IQueryable<Transaction> VisibleTo(this IQueryable<Transaction> transactions, int userId, bool canSeeAll) =>
            transactions.Where(t => canSeeAll || t.AgentUserId == userId || t.CreatedByUserId == userId);
    }
}
