using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Portföy veri erişimi. Görünürlük kuralı bu sorguların İÇİNDE uygulanır.
    // Müşterideki gibi IGenericDal'dan TÜREMEZ: filtresiz liste/okuma ve gerçek silme yoktur.
    public interface IPropertyDal
    {
        // Kullanıcının görebildiği, silinmemiş portföyler (filtre + sayfalama aynı sorguda)
        List<Property> GetVisibleList(int userId, bool canSeeAll, PropertyFilter filter, int skip, int take);

        int CountVisible(int userId, bool canSeeAll, PropertyFilter filter);

        // Görünmüyorsa veya silinmişse null. Salt okunur; sorumlu danışman bilgisiyle.
        // Mülk sahibi (OwnerContact) YÜKLENMEZ: sahibin görünürlüğü ayrıca kontrol edilir (IPropertyService.TGetVisibleOwner).
        Property? GetVisibleById(int id, int userId, bool canSeeAll);

        // Aynı kural, EF takip eder: sadece servis içindeki güncelleme/silme akışında kullanılır.
        Property? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll);

        // Sorumlu danışman olabilir mi: kayıtlı, aktif ve en az bir rolü olan (ofis çalışanı) kullanıcı
        bool IsActiveStaff(int userId);

        // Sorumlu danışman seçimi için: aktif ve rolü olan çalışanlar (sadece id ve ad; şifre vb. alanlar okunmaz)
        List<(int Id, string FullName)> GetActiveStaff();

        void Create(Property property);

        void Update(Property property);
    }
}
