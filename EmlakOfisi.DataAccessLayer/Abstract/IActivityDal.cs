using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Görüşme / işlem geçmişi veri erişimi.
    // Görünürlük bağlı olduğu müşteriden gelir; o kontrol serviste (ActivityManager) yapılır.
    // InstrumentTrade'den fark: generic yapıdan türemez, filtresiz liste yoktur (sadece müşteriye göre).
    public interface IActivityDal
    {
        // Bir müşterinin görüşmeleri, en yeni üstte (kaydı giren çalışanın adıyla birlikte)
        List<Activity> GetListByContact(int contactId);

        // Silme kontrolü için (EF takip eder)
        Activity? GetByIdForUpdate(int activityId);

        void Create(Activity activity);

        void Delete(Activity activity);
    }
}
