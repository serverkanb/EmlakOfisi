using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Görüşme geçmişi servisi. Kurallar (kullanıcı kararı, görev 10):
    // - Görme: müşteriyi görebilen, geçmişini de görür; göremeyen göremez.
    // - Ekleme: müşteriyi DÜZENLEYEBİLEN ekler (sahibi ve yöneticiler) -> IContactService.TCanEdit.
    // - Silme: notu yazan kişi veya yönetici (ve müşteriyi görebiliyor olmalı).
    public interface IActivityService
    {
        // Müşteri görünmüyorsa null (controller 404)
        List<Activity>? TGetForContact(int contactId, int userId, bool canSeeAll);

        bool TCanAdd(Contact contact, int userId, bool canSeeAll);

        // Ekleme yetkisi var mı (form geçersiz olsa bile önce bu kontrol edilir): NotFound/Forbidden/Success
        OperationResult TCheckAdd(int contactId, int userId, bool canSeeAll);

        bool TCanDelete(Activity activity, int userId, bool canSeeAll);

        // Kaydı giren kişi ve müşteri id'si serviste atanır; formdan gelmez
        OperationResult TAdd(int contactId, Activity activity, int userId, bool canSeeAll);

        // contactId: notun bağlı olduğu müşteri (sadece başarıda dolu; geri yönlendirme için)
        OperationResult TDelete(int activityId, int userId, bool canSeeAll, out int contactId);
    }
}
