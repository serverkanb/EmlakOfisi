using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Portföy servisi. IContactService gibi IGenericService'ten TÜREMEZ (filtresiz okuma yok).
    // Kurallar (docs/MIMARI_PLAN.md bölüm 3):
    // - Görme : yönetici, herkese açık, kaydı oluşturan veya sorumlu danışman
    // - Düzenleme: yönetici, kaydı oluşturan veya sorumlu danışman
    // - Silme : yönetici veya kaydı oluşturan (soft delete)
    // - Sorumlu danışmanı sadece yönetici değiştirir; danışmanın eklediği portföyün sorumlusu kendisidir.
    //   Sorumlu aktif ve rolü olan bir ofis çalışanı olmalı.
    // - Görünürlüğü düzenleyebilen herkes (sorumlu danışman dahil) değiştirebilir (kullanıcı kararı).
    // - Mülk sahibi sadece kullanıcının GÖREBİLDİĞİ bir kişi olabilir; gösterimde de ayrıca kontrol edilir.
    //   Mevcut sahibi göremeyen kullanıcı sahibi değiştiremez/kaldıramaz (mevcut sahip korunur).
    public interface IPropertyService
    {
        // page 1'den başlar, pageSize en fazla 100
        (List<Property> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            PropertyFilter filter, int page, int pageSize);

        Property? TGetVisibleById(int id, int userId, bool canSeeAll);

        bool TCanEdit(Property property, int userId, bool canSeeAll);
        bool TCanDelete(Property property, int userId, bool canSeeAll);

        // Mülk sahibi kullanıcıya görünüyorsa döner; yoksa veya görünmüyorsa null
        // (portföyü görmek, sahibini görmek anlamına gelmez)
        Contact? TGetVisibleOwner(Property property, int userId, bool canSeeAll);

        // Yöneticinin sorumlu danışman seçebileceği kişiler (aktif ve rolü olan çalışanlar)
        List<(int Id, string FullName)> TGetAssignableAgents();

        // Sahip, tarih, silinme durumu serviste atanır. Geçersiz değer (görünmeyen sahip, negatif fiyat,
        // geçersiz koordinat) -> Invalid
        OperationResult TCreate(Property property, int userId, bool canSeeAll);

        // Sadece izinli alanlar kopyalanır; sorumlu danışman sadece yönetici tarafından değişir
        OperationResult TUpdate(int id, Property changes, int userId, bool canSeeAll);

        OperationResult TSoftDelete(int id, int userId, bool canSeeAll);
    }
}
