using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Portföyle ilgilenen müşteriler. Generic yapıdan türemez, filtresiz liste yoktur.
    // Listeler karşı taraftaki kaydın görünürlüğünü SORGUNUN İÇİNDE uygular: portföyün listesinde sadece
    // kullanıcının görebildiği müşteriler, müşterinin listesinde sadece görebildiği portföyler çıkar.
    // Listenin bağlı olduğu kaydın (portföy ya da müşteri) görünür olduğunu servis ayrıca kontrol eder.
    public interface IPropertyInterestDal
    {
        // Müşteri ve ekleyen kişiyle birlikte, en yeni üstte
        List<PropertyInterest> GetListByProperty(int propertyId, int userId, bool canSeeAll);

        // Portföy ve ekleyen kişiyle birlikte, en yeni üstte
        List<PropertyInterest> GetListByContact(int contactId, int userId, bool canSeeAll);

        bool Exists(int propertyId, int contactId);

        // Silme kontrolü için (EF takip eder)
        PropertyInterest? GetByIdForUpdate(int propertyInterestId);

        // Aynı portföy-müşteri çifti zaten varsa (eşzamanlı iki ekleme, benzersiz indeks) false döner
        bool Create(PropertyInterest interest);

        void Delete(PropertyInterest interest);
    }
}
