using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Portföy fotoğrafları (görev 13). Kurallar:
    // - Görme: portföyü görebilen fotoğraflarını da görür; göremeyen fotoğrafın varlığını da öğrenemez (404).
    // - Yükleme, silme, kapak seçme: portföyü DÜZENLEYEBİLEN (oluşturan, sorumlu danışman, yönetici).
    // - Portföy başına en fazla MaxPerProperty fotoğraf. İlk fotoğraf otomatik kapak olur.
    // Dosyanın diske yazılması/silinmesi WebUI'deki PhotoFileStore'da; servis sadece kayıt ve yetkiyi yönetir.
    public interface IPropertyPhotoService
    {
        public const int MaxPerProperty = 30;

        // Portföy görünmüyorsa null
        List<PropertyPhoto>? TGetForProperty(int propertyId, int userId, bool canSeeAll);

        // Dosyayı göstermek için: fotoğrafın portföyü görünmüyorsa null
        PropertyPhoto? TGetVisible(int propertyPhotoId, int userId, bool canSeeAll);

        // Yükleme yetkisi ve kalan yer: NotFound / Forbidden / Invalid (sınır doldu) / Success.
        // remaining: daha kaç fotoğraf eklenebilir (sadece Success'te anlamlı)
        OperationResult TCheckUpload(int propertyId, int userId, bool canSeeAll, out int remaining);

        // Dosya diske yazıldıktan sonra kaydı ekler. Yükleyen, tarih, sıra ve kapak serviste atanır.
        OperationResult TAdd(int propertyId, string fileName, string contentType, string? originalName, int userId, bool canSeeAll);

        // fileName: silinen kaydın dosyası (sadece Success'te dolu; controller diskten siler)
        OperationResult TDelete(int propertyPhotoId, int userId, bool canSeeAll, out int propertyId, out string fileName);

        OperationResult TSetCover(int propertyPhotoId, int userId, bool canSeeAll, out int propertyId);
    }
}
