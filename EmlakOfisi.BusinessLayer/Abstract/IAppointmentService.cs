using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Randevu servisi (görev 14). Randevu danışmanın ajandasıdır; "Herkese Açık / Sadece Ben" seçimi yoktur.
    // - Görme ve düzenleme: yönetici, sorumlu danışman ya da randevuyu oluşturan
    // - Danışman sadece kendi adına randevu ekler; sorumluyu sadece yönetici seçer/değiştirir
    //   (seçilen kişi aktif ve rolü olan bir çalışan olmalı)
    // - Bağlı müşteri/portföy kullanıcının GÖREBİLDİĞİ bir kayıt olmalı. Mevcut bağlı kaydı göremeyen kullanıcı
    //   onu değiştiremez/kaldıramaz (gönderdiği değer yok sayılır, bağlantı korunur).
    // - Silme yoktur: randevu "İptal" yapılır.
    public interface IAppointmentService
    {
        // page 1'den başlar, pageSize en fazla 100
        (List<AppointmentRow> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            AppointmentFilter filter, int page, int pageSize);

        AppointmentRow? TGetVisibleById(int id, int userId, bool canSeeAll);

        bool TCanEdit(Appointment appointment, int userId, bool canSeeAll);

        // Sorumlu, oluşturan, tarih ve durum serviste atanır. Geçersiz bağlantı/sorumlu/tarih -> Invalid
        OperationResult TCreate(Appointment appointment, int userId, bool canSeeAll);

        // Sadece izinli alanlar kopyalanır
        OperationResult TUpdate(int id, Appointment changes, int userId, bool canSeeAll);

        // Planlandı / Yapıldı / İptal
        OperationResult TSetStatus(int id, AppointmentStatus status, int userId, bool canSeeAll);
    }
}
