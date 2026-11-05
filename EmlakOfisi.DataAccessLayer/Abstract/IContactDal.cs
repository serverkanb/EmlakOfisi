using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Müşteri veri erişimi. Görünürlük kuralı bu sorguların İÇİNDE uygulanır.
    // InstrumentTrade'den FARK: IGenericDal<Contact>'tan türemez. Böylece filtresiz GetList / GetById
    // ve gerçek silme (Delete) hiç yoktur; müşteri verisine görünürlük kuralı atlanarak ulaşılamaz.
    public interface IContactDal
    {
        // Kullanıcının görebildiği, silinmemiş müşteriler (arama + rol filtresi + sayfalama aynı sorguda)
        List<Contact> GetVisibleList(int userId, bool canSeeAll, string? nameSearch, string? phoneSearch,
            ContactRole? role, int skip, int take);

        int CountVisible(int userId, bool canSeeAll, string? nameSearch, string? phoneSearch, ContactRole? role);

        // Görünmüyorsa veya silinmişse null. Salt okunur (EF takip etmez): ekranda göstermek için.
        Contact? GetVisibleById(int id, int userId, bool canSeeAll);

        // Aynı kural, ama EF takip eder: sadece servis içindeki güncelleme/silme akışında kullanılır.
        Contact? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll);

        // Tekrar kayıt kontrolü için: aynı telefonla silinmemiş kayıt var mı (görünürlükten bağımsız).
        // Sadece id döner; kaydın kendisi görünürlük kontrolü olmadan dışarı verilmez.
        int? FindIdByPhone(string phoneNormalized, int? excludeContactId);

        void Create(Contact contact);

        void Update(Contact contact);
    }
}
