using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Tamamlanan işlemde portföy durumunun ne olduğu (ekranda mesaj için)
    public enum PropertyStatusUpdate
    {
        NotApplicable,   // işlem tamamlanmadı
        Updated,         // portföy Satıldı/Kiralandı yapıldı
        NotAllowed,      // kullanıcı portföyü düzenleyemiyor ya da portföy görünmüyor: elle güncellenmeli
        CheckProperty    // işlem tamamlanmışlıktan çıktı: portföy Satıldı/Kiralandı kaldı, elle kontrol edilmeli
    }

    // Satış/kiralama işlemleri (görev 18). Kullanıcı kararları:
    // - Görme ve düzenleme: yönetici, sorumlu danışman ya da işlemi oluşturan (komisyon içerdiği için başkası görmez)
    // - Danışman sadece kendi adına başlatır; sorumluyu sadece yönetici seçer/değiştirir (aktif ve rolü olan çalışan)
    // - Portföy ve müşteri, başlatan kişinin GÖREBİLDİĞİ kayıtlar olmalı. Görmediği mevcut müşteri bağlantısını
    //   değiştiremez (korunur). Portföy sonradan değiştirilemez.
    // - Komisyon: oran ve anlaşılan fiyat varsa tutar sunucuda hesaplanır (fiyat x oran / 100); yoksa girilen tutar.
    // - "Tamamlandı" olunca tamamlanma tarihi yazılır ve kullanıcı portföyü düzenleyebiliyorsa portföy Satıldı/Kiralandı olur.
    // - Silme yok: işlem "İptal" yapılır.
    public interface ITransactionService
    {
        (List<TransactionRow> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            TransactionFilter filter, int page, int pageSize);

        TransactionRow? TGetVisibleById(int id, int userId, bool canSeeAll);

        bool TCanEdit(Transaction transaction, int userId, bool canSeeAll);

        // Bu ay vb. aralıkta tamamlanan görünür işlemler (ana ekran)
        TransactionSummary TGetCompletedSummary(int userId, bool canSeeAll, DateTime from, DateTime to);

        // Portföy görünmüyor -> NotFound. Müşteri görünmüyor, geçersiz sorumlu/değer -> Invalid
        OperationResult TCreate(Transaction transaction, int userId, bool canSeeAll, out PropertyStatusUpdate propertyUpdate);

        OperationResult TUpdate(int id, Transaction changes, int userId, bool canSeeAll, out PropertyStatusUpdate propertyUpdate);

        OperationResult TSetStatus(int id, TransactionStatus status, int userId, bool canSeeAll, out PropertyStatusUpdate propertyUpdate);

        // Kontrat dosyası (görev 22): yükleme/kaldırma işlemi düzenleyebilene açık (aynı gün kuralı dahil).
        // Görmeyen NotFound, düzenleyemeyen Forbidden. Dosya diske yazılmadan önce TCheckContractEdit çağrılır.
        OperationResult TCheckContractEdit(int id, int userId, bool canSeeAll);

        // Başarılıysa replacedFileName eski dosyanın adıdır (varsa diskten silinmeli)
        OperationResult TSetContract(int id, string fileName, string originalName, string contentType,
            int userId, bool canSeeAll, out string? replacedFileName);

        OperationResult TRemoveContract(int id, int userId, bool canSeeAll, out string? removedFileName);
    }
}
