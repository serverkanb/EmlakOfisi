using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Tekrar kayıt kontrolü sonucu. Kayıt kullanıcıya görünmüyorsa id VERİLMEZ (bilgi sızmasın).
    public class DuplicatePhoneResult
    {
        public bool Exists { get; set; }
        public int? VisibleContactId { get; set; }
    }

    // Müşteri servisi. Bilinçli olarak IGenericService<Contact>'tan TÜREMEZ:
    // filtresiz TGetList / TGetById olmasın, her okuma görünürlük kuralından geçsin.
    // Güncelleme ve silme yetkiyi servis içinde kendisi kontrol eder.
    // userId = giriş yapan kullanıcı, canSeeAll = Admin veya Manager mı.
    public interface IContactService
    {
        // Liste + arama + rol filtresi + sayfalama (page 1'den başlar, pageSize en fazla 100)
        (List<Contact> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            string? search, ContactRole? role, int page, int pageSize);

        // Görünmüyorsa veya silinmişse null (controller 404 döner)
        Contact? TGetVisibleById(int id, int userId, bool canSeeAll);

        // Görünürlükten ayrı kurallar: düzenleme ve silme (ekranda buton göstermek için de kullanılır)
        bool TCanEdit(Contact contact, int userId, bool canSeeAll);
        bool TCanDelete(Contact contact, int userId, bool canSeeAll);

        // Yeni kayıt: sahibi, tarihleri ve silinme durumu serviste atanır; formdan gelmez
        void TCreate(Contact contact, int userId);

        // Sadece izin verilen alanlar (ad, soyad, telefonlar, e-posta, not, roller, görünürlük) kopyalanır
        OperationResult TUpdate(int id, Contact changes, int userId, bool canSeeAll);

        // Gerçek silme yok: IsDeleted = true
        OperationResult TSoftDelete(int id, int userId, bool canSeeAll);

        // Aynı telefonla kayıt var mı? Güncellemede kaydın kendisi excludeContactId ile hariç tutulur.
        DuplicatePhoneResult TCheckDuplicatePhone(string phone, int userId, bool canSeeAll, int? excludeContactId = null);
    }
}
