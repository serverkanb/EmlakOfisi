using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.DataAccessLayer.Abstract
{
    // Portföy fotoğrafları. Generic yapıdan türemez, filtresiz liste yoktur (sadece portföye göre).
    // Fotoğrafın görünürlüğü bağlı olduğu portföyden gelir; o kontrol serviste (PropertyPhotoManager) yapılır.
    public interface IPropertyPhotoDal
    {
        // Kapak önce, sonra yükleme sırası
        List<PropertyPhoto> GetListByProperty(int propertyId);

        int CountByProperty(int propertyId);

        // Salt okunur (dosya gösterimi için)
        PropertyPhoto? GetById(int propertyPhotoId);

        // Silme / kapak işlemleri için (EF takip eder)
        PropertyPhoto? GetByIdForUpdate(int propertyPhotoId);

        void Create(PropertyPhoto photo);

        void Delete(PropertyPhoto photo);

        // Portföyün kapağını tek fotoğrafa ayarlar (diğerlerinin kapak işareti kalkar)
        void SetCover(int propertyId, int propertyPhotoId);
    }
}
