using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Müşteri talepleri. Görünürlük bağlı müşteriden gelir (VisibilityRules.VisibleTo(Contact)) ve sorgunun İÇİNDE uygulanır.
    // Generic yapıdan TÜREMEZ: filtresiz okuma ve gerçek silme yok (talep "Kapandı" yapılır).
    public interface ICustomerRequestDal
    {
        List<CustomerRequestRow> GetVisibleList(int userId, bool canSeeAll, CustomerRequestFilter filter, int skip, int take);

        int CountVisible(int userId, bool canSeeAll, CustomerRequestFilter filter);

        // Bağlı müşteri görünmüyorsa (ya da silinmişse) null. Salt okunur.
        CustomerRequest? GetVisibleById(int id, int userId, bool canSeeAll);

        // Aynı kural, EF takip eder: sadece servis içindeki güncelleme akışında
        CustomerRequest? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll);

        void Create(CustomerRequest request);

        void Update(CustomerRequest request);
    }
}
