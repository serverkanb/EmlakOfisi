using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Müşteri talepleri (görev 17). Kurallar:
    // - Görme: talebi, bağlı müşteriyi görebilen görür.
    // - Ekleme/düzenleme/kapatma: müşteriyi düzenleyebilen (sahibi, yönetici) YA DA admin'in
    //   "başkasının müşterisine talep ekleyip düzenleyebilir" izni verdiği kişi (canEditOthers).
    //   İzin görünürlüğü değiştirmez: müşteri görünmüyorsa yine NotFound.
    // - Silme yok: talep "Kapandı" yapılır, yeniden açılabilir.
    public interface ICustomerRequestService
    {
        public const int MaxNotesLength = 2000;

        // page 1'den başlar, pageSize en fazla 100
        (List<CustomerRequestRow> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            CustomerRequestFilter filter, int page, int pageSize);

        CustomerRequest? TGetVisibleById(int id, int userId, bool canSeeAll);

        // DİKKAT: contact, kullanıcıya görünürlüğü kontrol edilmiş olmalı (TGetVisibleById ile okunmuş).
        // Bu metot görünürlüğe bakmaz; sadece "görebildiği müşteride düzenleyebilir mi" sorusunu cevaplar.
        bool TCanEdit(Contact contact, int userId, bool canSeeAll, bool canEditOthers);

        // Müşteri görünmüyor -> NotFound, düzenleyemiyor -> Forbidden
        OperationResult TCheckAdd(int contactId, int userId, bool canSeeAll, bool canEditOthers);

        // Müşteri, oluşturan, tarih ve durum serviste atanır. Geçersiz değer (negatif fiyat, min > max) -> Invalid
        OperationResult TCreate(int contactId, CustomerRequest request, int userId, bool canSeeAll, bool canEditOthers);

        // Sadece talep alanları kopyalanır (müşteri, oluşturan, tarih değişmez)
        OperationResult TUpdate(int id, CustomerRequest changes, int userId, bool canSeeAll, bool canEditOthers);

        OperationResult TSetStatus(int id, RequestStatus status, int userId, bool canSeeAll, bool canEditOthers, out int contactId);
    }
}
