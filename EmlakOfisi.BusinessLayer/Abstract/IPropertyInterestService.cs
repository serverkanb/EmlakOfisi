using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Portföyle ilgilenen müşteriler. Kurallar (kullanıcı kararı, görev 12c):
    // - Görme: satırı görmek için hem portföyü hem müşteriyi görebiliyor olmak gerekir. Görünmeyen
    //   müşteri/portföy listede hiç çıkmaz ("+N gizli" gibi bir sayı da verilmez).
    // - Ekleme: portföyü gören herkes, görebildiği bir müşteriyi ekleyebilir.
    // - Silme: ekleyen kişi, portföyü düzenleyebilen (oluşturan, sorumlu) ya da yönetici.
    public interface IPropertyInterestService
    {
        public const int MaxNoteLength = 500;

        // Portföy görünmüyorsa null (controller 404)
        List<PropertyInterest>? TGetForProperty(int propertyId, int userId, bool canSeeAll);

        // Müşteri görünmüyorsa null
        List<PropertyInterest>? TGetForContact(int contactId, int userId, bool canSeeAll);

        // property: satırın bağlı olduğu portföy (görünürlük kontrolünden geçmiş)
        bool TCanDelete(PropertyInterest interest, Property property, int userId, bool canSeeAll);

        // Portföy ya da müşteri görünmüyorsa NotFound; kişi zaten listedeyse ya da not çok uzunsa Invalid.
        // Ekleyen kişi ve tarih serviste atanır.
        OperationResult TAdd(int propertyId, int contactId, string? note, int userId, bool canSeeAll);

        // propertyId/contactId: satırın gerçekten bağlı olduğu kayıtlar (geri yönlendirme için; formdan alınmaz)
        OperationResult TDelete(int propertyInterestId, int userId, bool canSeeAll, out int propertyId, out int contactId);
    }
}
