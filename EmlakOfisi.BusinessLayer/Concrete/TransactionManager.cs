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
            NormalizeLease(t);
            // Kontrat dosyası sadece TSetContract ile eklenir (formdan gelen değer yok sayılır)
            t.ContractFileName = t.ContractOriginalName = t.ContractContentType = null;
            t.ContractUploadedAt = null;
            t.ContractUploadedByUserId = null;
            t.ContractUploadedByUser = null;
            if (!Enum.IsDefined(t.Status)) t.Status = TransactionStatus.Offer;
            ApplyCommission(t, t.CommissionAmount);
            t.CompletedAt = t.Status == TransactionStatus.Completed ? DateTime.Now : null;
            t.ClosedAt = IsClosed(t.Status) ? DateTime.Now : null;
            if (!canSeeAll) t.AgentShareRate = null;   // prim oranını elle sadece yönetici yazar
            ApplyAgentShare(t);

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
            var agentChanged = false;
            if (canSeeAll && changes.AgentUserId != 0 && changes.AgentUserId != t.AgentUserId)
            {
                if (!IsAssignable(changes.AgentUserId))
                    return OperationResult.Invalid;
                agentId = changes.AgentUserId;
                agentChanged = true;
            }

            var previous = t.Status;
            var wasCompleted = previous == TransactionStatus.Completed;
            t.CustomerContactId = contactId;
            t.AgentUserId = agentId;
            t.OfferPrice = changes.OfferPrice;
            t.AgreedPrice = changes.AgreedPrice;
            t.CommissionRate = changes.CommissionRate;
            t.Notes = changes.Notes;
            t.LeaseStartDate = changes.LeaseStartDate;
            t.LeaseEndDate = changes.LeaseEndDate;
            NormalizeLease(t);
            ApplyCommission(t, changes.CommissionAmount);
            if (Enum.IsDefined(changes.Status)) t.Status = changes.Status;
            UpdateCompletion(t, previous);
            if (canSeeAll)
            {
                // Yönetici oranı elle yazabilir; boş bırakırsa danışmanın oranı kullanılır.
                // Sorumlu değiştiyse ve oran elle değiştirilmediyse yeni danışmanın oranı alınır.
                var rateUntouched = changes.AgentShareRate == t.AgentShareRate;
                t.AgentShareRate = agentChanged && rateUntouched ? null : changes.AgentShareRate;
            }
            ApplyAgentShare(t);

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
            ApplyAgentShare(t);
            _transactionDal.Update(t);

            propertyUpdate = AfterStatusChange(t, previous == TransactionStatus.Completed, userId, canSeeAll);
            return OperationResult.Success;
        }

        // ---------------- Kontrat dosyası ----------------

        public OperationResult TCheckContractEdit(int id, int userId, bool canSeeAll)
        {
            var t = _transactionDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (t == null)
                return OperationResult.NotFound;
            return TCanEdit(t, userId, canSeeAll) ? OperationResult.Success : OperationResult.Forbidden;
        }

        public OperationResult TSetContract(int id, string fileName, string originalName, string contentType,
            int userId, bool canSeeAll, out string? replacedFileName)
        {
            replacedFileName = null;
            var t = _transactionDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (t == null)
                return OperationResult.NotFound;
            if (!TCanEdit(t, userId, canSeeAll))
                return OperationResult.Forbidden;
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType))
                return OperationResult.Invalid;

            replacedFileName = t.ContractFileName;
            t.ContractFileName = fileName;
            t.ContractOriginalName = CleanOriginalName(originalName, contentType);
            t.ContractContentType = contentType;
            t.ContractUploadedAt = DateTime.Now;
            t.ContractUploadedByUserId = userId;
            _transactionDal.Update(t);
            return OperationResult.Success;
        }

        public OperationResult TRemoveContract(int id, int userId, bool canSeeAll, out string? removedFileName)
        {
            removedFileName = null;
            var t = _transactionDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (t == null)
                return OperationResult.NotFound;
            if (!TCanEdit(t, userId, canSeeAll))
                return OperationResult.Forbidden;

            removedFileName = t.ContractFileName;
            t.ContractFileName = t.ContractOriginalName = t.ContractContentType = null;
            t.ContractUploadedAt = null;
            t.ContractUploadedByUserId = null;
            _transactionDal.Update(t);
            return OperationResult.Success;
        }

        // Kullanıcının dosya adı sadece gösterim için: yol kısmı ve kontrol karakterleri atılır, 200 karakterle sınırlanır.
        // Uzantı içerikten tespit edilen türe göre yazılır ("kontrat.html" adıyla yüklenen PDF indirilince .pdf olur).
        private static string CleanOriginalName(string? name, string contentType)
        {
            var clean = Path.GetFileName((name ?? string.Empty).Replace('\\', '/'));
            clean = new string(clean.Where(c => !char.IsControl(c)).ToArray()).Trim();
            clean = Path.GetFileNameWithoutExtension(clean).Trim();
            if (clean.Length == 0) clean = "kontrat";
            if (clean.Length > 190) clean = clean[..190];
            var extension = contentType switch
            {
                "application/pdf" => ".pdf",
                "image/png" => ".png",
                _ => ".jpg"
            };
            return clean + extension;
        }

        // ---------------- Yardımcılar ----------------

        // Kira tarihleri sadece kiralamada anlamlı; satışta boşaltılır
        private static void NormalizeLease(Transaction t)
        {
            if (t.Type != ListingType.Rent)
            {
                t.LeaseStartDate = null;
                t.LeaseEndDate = null;
                return;
            }
            t.LeaseStartDate = t.LeaseStartDate?.Date;
            t.LeaseEndDate = t.LeaseEndDate?.Date;
        }

        private static bool ValuesValid(Transaction t) =>
            (t.LeaseStartDate == null || t.LeaseEndDate == null || t.LeaseEndDate.Value.Date > t.LeaseStartDate.Value.Date) &&
            (t.OfferPrice == null || t.OfferPrice >= 0) &&
            (t.AgreedPrice == null || t.AgreedPrice >= 0) &&
            (t.CommissionRate == null || (t.CommissionRate >= 0 && t.CommissionRate <= 100)) &&
            (t.CommissionAmount == null || t.CommissionAmount >= 0) &&
            (t.AgentShareRate == null || (t.AgentShareRate >= 0 && t.AgentShareRate <= 100)) &&
            (t.Notes == null || t.Notes.Length <= 2000);

        // Oran ve anlaşılan fiyat varsa tutar hesaplanır; yoksa elle girilen tutar kullanılır
        private static void ApplyCommission(Transaction t, decimal? enteredAmount)
        {
            t.CommissionAmount = CommissionCalculator.Calculate(t.AgreedPrice, t.CommissionRate) ?? enteredAmount;
        }

        // Danışman primi: tamamlanmış işlemde oran yoksa danışmanın güncel oranı kopyalanır (sonradan değişmez);
        // tutar = komisyon x oran / 100. Tamamlanmamış işlemde tutar boş.
        private void ApplyAgentShare(Transaction t)
        {
            if (t.Status != TransactionStatus.Completed)
            {
                t.AgentShareAmount = null;
                return;
            }
            t.AgentShareRate ??= _transactionDal.GetAgentShareRate(t.AgentUserId);
            t.AgentShareAmount = CommissionCalculator.Calculate(t.CommissionAmount, t.AgentShareRate);
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
