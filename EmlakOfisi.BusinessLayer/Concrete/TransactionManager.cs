using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Concrete
{
    public class TransactionManager : ITransactionService
    {
        private readonly ITransactionDal _transactionDal;
        private readonly IContactService _contactService;
        private readonly IPropertyService _propertyService;

        // Portföy/müşteri görünürlüğü, portföy düzenleme ve atanabilir çalışan kuralları ilgili servislerde (tek yer)
        public TransactionManager(ITransactionDal transactionDal, IContactService contactService, IPropertyService propertyService)
        {
            _transactionDal = transactionDal;
            _contactService = contactService;
            _propertyService = propertyService;
        }

        public (List<TransactionRow> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            TransactionFilter filter, int page, int pageSize)
        {
            page = Math.Clamp(page, 1, 100_000);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var items = _transactionDal.GetVisibleList(userId, canSeeAll, filter, (page - 1) * pageSize, pageSize);
            var total = _transactionDal.CountVisible(userId, canSeeAll, filter);
            return (items, total);
        }

        public TransactionRow? TGetVisibleById(int id, int userId, bool canSeeAll) =>
            _transactionDal.GetVisibleById(id, userId, canSeeAll);

        // Görünürlükten AYRI kural (CLAUDE.md kural 3). Kullanıcı kararı: tamamlanan ya da iptal edilen işlemi
        // danışman sadece kapandığı GÜN değiştirebilir; ertesi günden itibaren sadece yönetici.
        public bool TCanEdit(Transaction transaction, int userId, bool canSeeAll)
        {
            if (canSeeAll)
                return true;
            if (transaction.AgentUserId != userId && transaction.CreatedByUserId != userId)
                return false;
            return !IsClosed(transaction.Status) || transaction.ClosedAt?.Date == DateTime.Today;
        }

        private static bool IsClosed(TransactionStatus s) =>
            s == TransactionStatus.Completed || s == TransactionStatus.Cancelled;

        public TransactionSummary TGetCompletedSummary(int userId, bool canSeeAll, DateTime from, DateTime to) =>
            _transactionDal.GetCompletedSummary(userId, canSeeAll, from, to);

        public OperationResult TCreate(Transaction t, int userId, bool canSeeAll, out PropertyStatusUpdate propertyUpdate)
        {
            propertyUpdate = PropertyStatusUpdate.NotApplicable;

            var property = _propertyService.TGetVisibleById(t.PropertyId, userId, canSeeAll);
            if (property == null)
                return OperationResult.NotFound;
            if (_contactService.TGetVisibleById(t.CustomerContactId, userId, canSeeAll) == null)
                return OperationResult.Invalid;
            if (!ValuesValid(t))
                return OperationResult.Invalid;

            // Danışman sadece kendi adına; yönetici aktif bir çalışanı seçebilir
            if (!canSeeAll || t.AgentUserId == 0)
                t.AgentUserId = userId;
            else if (t.AgentUserId != userId && !IsAssignable(t.AgentUserId))
                return OperationResult.Invalid;

            t.TransactionId = 0;
            t.Type = property.ListingType;   // işlem türü portföyün ilan tipinden gelir
            t.CreatedByUserId = userId;
            t.CreatedAt = DateTime.Now;
            t.Property = null;
            t.CustomerContact = null;
            t.AgentUser = null;
            t.CreatedByUser = null;
            if (!Enum.IsDefined(t.Status)) t.Status = TransactionStatus.Offer;
            ApplyCommission(t, t.CommissionAmount);
            t.CompletedAt = t.Status == TransactionStatus.Completed ? DateTime.Now : null;
            t.ClosedAt = IsClosed(t.Status) ? DateTime.Now : null;

            _transactionDal.Create(t);
            if (t.Status == TransactionStatus.Completed)
                propertyUpdate = MarkProperty(t, userId, canSeeAll);
            return OperationResult.Success;
        }

        public OperationResult TUpdate(int id, Transaction changes, int userId, bool canSeeAll, out PropertyStatusUpdate propertyUpdate)
        {
            propertyUpdate = PropertyStatusUpdate.NotApplicable;

            var t = _transactionDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (t == null)
                return OperationResult.NotFound;
            if (!TCanEdit(t, userId, canSeeAll))
                return OperationResult.Forbidden;
            if (!ValuesValid(changes))
                return OperationResult.Invalid;

            // Müşteri: mevcudu göremeyen değiştiremez (korunur); yeni müşteri görünür olmalı
            var currentHidden = _contactService.TGetVisibleById(t.CustomerContactId, userId, canSeeAll) == null;
            var contactId = t.CustomerContactId;
            if (!currentHidden && changes.CustomerContactId != 0 && changes.CustomerContactId != t.CustomerContactId)
            {
                if (_contactService.TGetVisibleById(changes.CustomerContactId, userId, canSeeAll) == null)
                    return OperationResult.Invalid;
                contactId = changes.CustomerContactId;
            }

            // Sorumlu sadece yönetici tarafından ve aktif bir çalışana değişir; aynı değer gelirse dokunulmaz
            var agentId = t.AgentUserId;
            if (canSeeAll && changes.AgentUserId != 0 && changes.AgentUserId != t.AgentUserId)
            {
                if (!IsAssignable(changes.AgentUserId))
                    return OperationResult.Invalid;
                agentId = changes.AgentUserId;
            }

            var previous = t.Status;
            var wasCompleted = previous == TransactionStatus.Completed;
            t.CustomerContactId = contactId;
            t.AgentUserId = agentId;
            t.OfferPrice = changes.OfferPrice;
            t.AgreedPrice = changes.AgreedPrice;
            t.CommissionRate = changes.CommissionRate;
            t.Notes = changes.Notes;
            ApplyCommission(t, changes.CommissionAmount);
            if (Enum.IsDefined(changes.Status)) t.Status = changes.Status;
            UpdateCompletion(t, previous);

            _transactionDal.Update(t);
            propertyUpdate = AfterStatusChange(t, wasCompleted, userId, canSeeAll);
            return OperationResult.Success;
        }

        public OperationResult TSetStatus(int id, TransactionStatus status, int userId, bool canSeeAll, out PropertyStatusUpdate propertyUpdate)
        {
            propertyUpdate = PropertyStatusUpdate.NotApplicable;

            var t = _transactionDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (t == null)
                return OperationResult.NotFound;
            if (!TCanEdit(t, userId, canSeeAll))
                return OperationResult.Forbidden;
            if (!Enum.IsDefined(status))
                return OperationResult.Invalid;

            var previous = t.Status;
            t.Status = status;
            UpdateCompletion(t, previous);
            _transactionDal.Update(t);

            propertyUpdate = AfterStatusChange(t, previous == TransactionStatus.Completed, userId, canSeeAll);
            return OperationResult.Success;
        }

        // ---------------- Yardımcılar ----------------

        private static bool ValuesValid(Transaction t) =>
            (t.OfferPrice == null || t.OfferPrice >= 0) &&
            (t.AgreedPrice == null || t.AgreedPrice >= 0) &&
            (t.CommissionRate == null || (t.CommissionRate >= 0 && t.CommissionRate <= 100)) &&
            (t.CommissionAmount == null || t.CommissionAmount >= 0) &&
            (t.Notes == null || t.Notes.Length <= 2000);

        // Oran ve anlaşılan fiyat varsa tutar hesaplanır; yoksa elle girilen tutar kullanılır
        private static void ApplyCommission(Transaction t, decimal? enteredAmount)
        {
            t.CommissionAmount = CommissionCalculator.Calculate(t.AgreedPrice, t.CommissionRate) ?? enteredAmount;
        }

        // Tamamlanma ve kapanış tarihleri: duruma ilk girildiğinde yazılır, durumdan çıkınca silinir
        private static void UpdateCompletion(Transaction t, TransactionStatus previous)
        {
            if (t.Status == TransactionStatus.Completed && previous != TransactionStatus.Completed)
                t.CompletedAt = DateTime.Now;
            else if (t.Status != TransactionStatus.Completed)
                t.CompletedAt = null;

            if (IsClosed(t.Status) && t.Status != previous)
                t.ClosedAt = DateTime.Now;
            else if (!IsClosed(t.Status))
                t.ClosedAt = null;
        }

        // Yeni tamamlandıysa portföy işaretlenir; tamamlanmışlıktan çıktıysa portföy durumu kontrol edilmeli (geri alınmaz)
        private PropertyStatusUpdate AfterStatusChange(Transaction t, bool wasCompleted, int userId, bool canSeeAll)
        {
            if (!wasCompleted && t.Status == TransactionStatus.Completed)
                return MarkProperty(t, userId, canSeeAll);
            if (wasCompleted && t.Status != TransactionStatus.Completed)
                return PropertyStatusUpdate.CheckProperty;
            return PropertyStatusUpdate.NotApplicable;
        }

        // Tamamlanan işlemin portföyünü Satıldı/Kiralandı yapar (sadece kullanıcı portföyü düzenleyebiliyorsa)
        private PropertyStatusUpdate MarkProperty(Transaction t, int userId, bool canSeeAll)
        {
            var status = t.Type == ListingType.Rent ? PropertyStatus.Rented : PropertyStatus.Sold;
            return _propertyService.TSetStatus(t.PropertyId, status, userId, canSeeAll) == OperationResult.Success
                ? PropertyStatusUpdate.Updated
                : PropertyStatusUpdate.NotAllowed;
        }

        private bool IsAssignable(int agentId) =>
            _propertyService.TGetAssignableAgents().Any(a => a.Id == agentId);
    }
}
