using System.Globalization;
using System.Text.RegularExpressions;
using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Helpers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Satış/kiralama işlemleri ve komisyon (görev 18). Kurallar serviste (TransactionManager).
    // Göremeyen 404, düzenleyemeyen 403; yetki form doğrulamasından ÖNCE. Silme yok, "İptal" var.
    [Authorize(Roles = RoleNames.Staff)]
    public class TransactionController : Controller
    {
        public const int PageSize = 20;
        public const int MaxChoices = 100;

        private readonly ITransactionService _transactionService;
        private readonly IPropertyService _propertyService;
        private readonly IContactService _contactService;
        private readonly DocumentFileStore _documents;
        private readonly ILogger<TransactionController> _logger;

        public TransactionController(ITransactionService transactionService, IPropertyService propertyService,
            IContactService contactService, DocumentFileStore documents, ILogger<TransactionController> logger)
        {
            _transactionService = transactionService;
            _propertyService = propertyService;
            _contactService = contactService;
            _documents = documents;
            _logger = logger;
        }

        // ---------------- Liste ----------------

        [HttpGet]
        public IActionResult Index(TransactionScope scope = TransactionScope.Open, bool mine = false, int page = 1)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            if (!Enum.IsDefined(scope)) scope = TransactionScope.Open;
            page = Math.Clamp(page, 1, 100_000);

            var (items, total) = _transactionService.TGetVisibleList(userId, canSeeAll,
                new TransactionFilter { Scope = scope, OnlyMine = mine }, page, PageSize);

            var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            if (page > lastPage && total > 0)
                return RedirectToAction(nameof(Index), new { scope, mine, page = lastPage });

            return View(new TransactionListViewModel
            {
                Items = items.Select(r => TransactionItemViewModel.From(r, userId,
                    _transactionService.TCanEdit(r.Transaction, userId, canSeeAll), canSeeAll)).ToList(),
                Scope = scope, Mine = mine, CanSeeAll = canSeeAll,
                Page = page, PageSize = PageSize, TotalCount = total
            });
        }

        // ---------------- Başlatma (portföy detayından) ----------------

        [HttpGet]
        public IActionResult Create(int propertyId, int? contactId)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            var property = _propertyService.TGetVisibleById(propertyId, userId, canSeeAll);
            if (property == null)
                return NotFound();

            var model = new TransactionFormViewModel { AgentUserId = userId };
            if (contactId != null && _contactService.TGetVisibleById(contactId.Value, userId, canSeeAll) != null)
                model.CustomerContactId = contactId.Value;

            ViewBag.Options = BuildOptions(property, null, model.CustomerContactId);
            return View(model);
        }

        [HttpPost]
        public IActionResult Create(int propertyId, TransactionFormViewModel model)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            var property = _propertyService.TGetVisibleById(propertyId, userId, canSeeAll);
            if (property == null)
                return NotFound();

            var transaction = ValidateAndMap(model, requireCustomer: true, property.ListingType);
            if (transaction == null)
            {
                ViewBag.Options = BuildOptions(property, null, model.CustomerContactId);
                return View(model);
            }

            transaction.PropertyId = propertyId;
            var result = _transactionService.TCreate(transaction, userId, canSeeAll, out var propertyUpdate);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result != OperationResult.Success)
            {
                ModelState.AddModelError("", "Seçilen müşteri, danışman ya da tutarlar geçersiz.");
                ViewBag.Options = BuildOptions(property, null, model.CustomerContactId);
                return View(model);
            }

            SetResultMessage("İşlem kaydedildi.", propertyUpdate);
            return RedirectToAction("Detail", "Property", new { id = propertyId }, "islemler");
        }

        // ---------------- Düzenleme ----------------

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            var row = _transactionService.TGetVisibleById(id, userId, canSeeAll);
            if (row == null)
                return NotFound();
            if (!_transactionService.TCanEdit(row.Transaction, userId, canSeeAll))
                return Forbid();

            var t = row.Transaction;
            ViewBag.Options = BuildOptions(row.Property, row, t.CustomerContactId);
            return View(new TransactionFormViewModel
            {
                // Gizli müşterinin id'si forma konmaz (serviste korunur)
                CustomerContactId = row.ContactHidden ? 0 : t.CustomerContactId,
                Status = t.Status,
                OfferPriceText = t.OfferPrice?.ToString("0"),
                AgreedPriceText = t.AgreedPrice?.ToString("0"),
                CommissionRateText = PropertyLabels.PercentText(t.CommissionRate),
                CommissionAmountText = t.CommissionAmount?.ToString("0"),
                AgentShareRateText = canSeeAll ? PropertyLabels.PercentText(t.AgentShareRate) : null,   // oran sadece yöneticiye
                AgentUserId = t.AgentUserId,
                LeaseStartDate = t.LeaseStartDate,
                LeaseEndDate = t.LeaseEndDate,
                Notes = t.Notes
            });
        }

        [HttpPost]
        public IActionResult Edit(int id, TransactionFormViewModel model)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            // Önce yetki (form hatalarından önce): göremeyen 404, düzenleyemeyen 403
            var row = _transactionService.TGetVisibleById(id, userId, canSeeAll);
            if (row == null)
                return NotFound();
            if (!_transactionService.TCanEdit(row.Transaction, userId, canSeeAll))
                return Forbid();

            var changes = ValidateAndMap(model, requireCustomer: !row.ContactHidden, row.Transaction.Type);
            if (changes == null)
            {
                ViewBag.Options = BuildOptions(row.Property, row, model.CustomerContactId);
                return View(model);
            }

            var result = _transactionService.TUpdate(id, changes, userId, canSeeAll, out var propertyUpdate);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();
            if (result == OperationResult.Invalid)
            {
                ModelState.AddModelError("", "Seçilen müşteri, danışman ya da tutarlar geçersiz.");
                ViewBag.Options = BuildOptions(row.Property, row, model.CustomerContactId);
                return View(model);
            }

            SetResultMessage("İşlem güncellendi.", propertyUpdate);
            return RedirectToAction(nameof(Index));
        }

        // ---------------- Durum ----------------

        [HttpPost]
        public IActionResult SetStatus(int id, TransactionStatus status, TransactionScope scope = TransactionScope.Open)
        {
            var result = _transactionService.TSetStatus(id, status, User.GetUserId(), User.CanSeeAll(), out var propertyUpdate);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            if (result == OperationResult.Invalid)
                TempData["Error"] = "Geçersiz durum.";
            else
                SetResultMessage($"İşlem durumu: {TransactionLabels.Status(status)}.", propertyUpdate);

            return RedirectToAction(nameof(Index), new { scope = Enum.IsDefined(scope) ? scope : TransactionScope.Open });
        }

        // ---------------- Kontrat dosyası (görev 22) ----------------

        // Dosya sadece işlemi GÖREBİLEN kişiye (komisyon/kişisel bilgi içerebilir); adresle doğrudan açılamaz
        [HttpGet]
        public IActionResult Contract(int id)
        {
            var row = _transactionService.TGetVisibleById(id, User.GetUserId(), User.CanSeeAll());
            var t = row?.Transaction;
            if (t?.ContractFileName == null || t.ContractContentType == null)
                return NotFound();

            var path = _documents.GetPath(t.ContractFileName);
            if (path == null)
                return NotFound();

            // Fotoğraflardaki gibi: her açılışta yetki kontrolü (no-cache), tip tahmini yok (nosniff; tür sunucunun
            // tespit ettiği PDF/JPG/PNG). CSP sandbox sadece resimlerde: Chrome/Firefox'un PDF görüntüleyicisi
            // sandbox'lı belgede açılmıyor.
            Response.Headers.CacheControl = "private, no-cache";
            Response.Headers.XContentTypeOptions = "nosniff";
            if (t.ContractContentType != "application/pdf")
                Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            // Türkçe karakterli ad için filename* (UTF-8) de yazılır
            var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("inline");
            disposition.SetHttpFileName(t.ContractOriginalName ?? "kontrat" + Path.GetExtension(t.ContractFileName));
            Response.Headers.ContentDisposition = disposition.ToString();
            return PhysicalFile(path, t.ContractContentType);
        }

        // İstek gövdesi 10 MB + form payıyla sınırlı (daha büyüğü action'a gelmeden reddedilir; geçici diske yazılmaz)
        public const long UploadRequestLimit = ContractFileValidator.MaxBytes + 512 * 1024;

        [HttpPost]
        [RequestSizeLimit(UploadRequestLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
        public async Task<IActionResult> UploadContract(int id, IFormFile? file)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            // Önce yetki (dosyaya bakmadan): göremeyen 404, düzenleyemeyen 403
            var check = _transactionService.TCheckContractEdit(id, userId, canSeeAll);
            if (check == OperationResult.NotFound)
                return NotFound();
            if (check == OperationResult.Forbidden)
                return Forbid();

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Lütfen bir dosya seçin.";
                return BackToEdit(id);
            }
            if (file.Length > ContractFileValidator.MaxBytes)
            {
                TempData["Error"] = "Kontrat dosyası en fazla 10 MB olabilir.";
                return BackToEdit(id);
            }

            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var read = await stream.ReadAsync(header);
            var type = ContractFileValidator.Detect(header.AsSpan(0, read));
            if (type == null)
            {
                TempData["Error"] = "Sadece PDF, JPG ya da PNG dosyası yüklenebilir.";
                return BackToEdit(id);
            }

            stream.Position = 0;
            string fileName;
            try
            {
                fileName = await _documents.SaveAsync(id, stream, type.Value.Extension);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Kontrat dosyası diske yazılamadı (işlem {TransactionId}).", id);
                TempData["Error"] = "Dosya sunucuya kaydedilemedi. Lütfen yöneticinize bildirin.";
                return BackToEdit(id);
            }

            OperationResult result;
            string? replaced;
            try
            {
                result = _transactionService.TSetContract(id, fileName, file.FileName, type.Value.ContentType,
                    userId, canSeeAll, out replaced);
            }
            catch
            {
                DeleteQuietly(fileName);   // kayıt yazılamadıysa dosya sahipsiz kalmasın (asıl hata kaybolmadan)
                throw;
            }

            if (result != OperationResult.Success)
            {
                DeleteQuietly(fileName);
                if (result == OperationResult.NotFound) return NotFound();
                if (result == OperationResult.Forbidden) return Forbid();
                TempData["Error"] = "Kontrat dosyası eklenemedi.";
                return BackToEdit(id);
            }

            DeleteQuietly(replaced);
            TempData["Success"] = replaced == null ? "Kontrat dosyası eklendi." : "Kontrat dosyası değiştirildi.";
            return BackToEdit(id);
        }

        [HttpPost]
        public IActionResult RemoveContract(int id)
        {
            var result = _transactionService.TRemoveContract(id, User.GetUserId(), User.CanSeeAll(), out var removed);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            if (removed == null)
            {
                TempData["Error"] = "Bu işlemde kontrat dosyası yok.";
                return BackToEdit(id);
            }
            DeleteQuietly(removed);
            TempData["Success"] = "Kontrat dosyası kaldırıldı.";
            return BackToEdit(id);
        }

        private void DeleteQuietly(string? fileName)
        {
            if (fileName == null) return;
            try
            {
                _documents.Delete(fileName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Kayıt güncellendi; dosya kalırsa sadece yer kaplar, kimseye gösterilmez
                _logger.LogWarning(ex, "Eski kontrat dosyası silinemedi: {FileName}", fileName);
            }
        }

        private IActionResult BackToEdit(int id) =>
            RedirectToAction(nameof(Edit), null, new { id }, "kontrat");

        // ---------------- Yardımcılar ----------------

        private void SetResultMessage(string message, PropertyStatusUpdate propertyUpdate)
        {
            TempData["Success"] = propertyUpdate == PropertyStatusUpdate.Updated
                ? message + " Portföy durumu da güncellendi."
                : message;
            if (propertyUpdate == PropertyStatusUpdate.NotAllowed)
                TempData["Error"] = "İşlem tamamlandı, ancak portföy durumu değişmedi (portföyü düzenleme yetkiniz yok ya da portföy silinmiş). Gerekirse portföyün sorumlusuna bildirin.";
            else if (propertyUpdate == PropertyStatusUpdate.CheckProperty)
                TempData["Error"] = "İşlem artık tamamlanmış değil. Portföy Satıldı/Kiralandı olarak kaldı; portföy durumunu kontrol edin.";
        }

        private Transaction? ValidateAndMap(TransactionFormViewModel m, bool requireCustomer, ListingType type)
        {
            decimal? Money(string? text, string field)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                if (PropertyLabels.TryParsePrice(text, out var v)) return v;
                ModelState.AddModelError(field, "Sadece rakam girin (ör. 5000000).");
                return null;
            }

            var offer = Money(m.OfferPriceText, nameof(m.OfferPriceText));
            var agreed = Money(m.AgreedPriceText, nameof(m.AgreedPriceText));
            var amount = Money(m.CommissionAmountText, nameof(m.CommissionAmountText));

            decimal? Percent(string? text, string field)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                if (PropertyLabels.TryParsePercent(text, out var v)) return v;
                ModelState.AddModelError(field, "Oranı 0 ile 100 arasında girin (ör. 2 ya da 2,5).");
                return null;
            }

            var rate = Percent(m.CommissionRateText, nameof(m.CommissionRateText));
            // Prim oranını elle sadece yönetici yazar (danışmanın gönderdiği değer serviste de yok sayılır)
            var shareRate = User.CanSeeAll() ? Percent(m.AgentShareRateText, nameof(m.AgentShareRateText)) : null;

            if (requireCustomer && m.CustomerContactId <= 0)
                ModelState.AddModelError(nameof(m.CustomerContactId), "Müşteri seçin.");
            if (!Enum.IsDefined(m.Status))
                ModelState.AddModelError(nameof(m.Status), "Geçersiz durum.");

            // Kira tarihleri sadece kiralamada (satışta gelen değer yok sayılır, serviste de boşaltılır)
            DateTime? leaseStart = null, leaseEnd = null;
            if (type == ListingType.Rent)
            {
                leaseStart = m.LeaseStartDate?.Date;
                leaseEnd = m.LeaseEndDate?.Date;
                var min = new DateTime(2000, 1, 1);
                var max = DateTime.Today.AddYears(30);
                if (leaseStart != null && (leaseStart < min || leaseStart > max))
                    ModelState.AddModelError(nameof(m.LeaseStartDate), "Geçerli bir tarih girin.");
                if (leaseEnd != null && (leaseEnd < min || leaseEnd > max))
                    ModelState.AddModelError(nameof(m.LeaseEndDate), "Geçerli bir tarih girin.");
                if (leaseStart != null && leaseEnd != null && leaseEnd <= leaseStart)
                    ModelState.AddModelError(nameof(m.LeaseEndDate), "Kira bitişi başlangıçtan sonra olmalıdır.");
            }

            if (!ModelState.IsValid)
                return null;

            return new Transaction
            {
                CustomerContactId = m.CustomerContactId,
                Status = m.Status,
                OfferPrice = offer,
                AgreedPrice = agreed,
                CommissionRate = rate,
                AgentShareRate = shareRate,
                CommissionAmount = amount,
                AgentUserId = m.AgentUserId,
                LeaseStartDate = leaseStart,
                LeaseEndDate = leaseEnd,
                Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes.Trim()
            };
        }

        // Müşteri seçenekleri sadece kullanıcının GÖREBİLDİĞİ kişilerden (satışta alıcılar, kiralıkta kiracılar)
        private TransactionFormOptions BuildOptions(Property? property, TransactionRow? existing, int contactId)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            var type = property?.ListingType ?? existing?.Transaction.Type ?? ListingType.Sale;
            var role = type == ListingType.Rent ? ContactRole.Tenant : ContactRole.Buyer;

            var customers = _contactService.TGetVisibleList(userId, canSeeAll, null, role, 1, MaxChoices).Items
                .ToDictionary(c => c.ContactId, c => $"{c.Name} {c.Surname}");
            if (contactId > 0 && !customers.ContainsKey(contactId))
            {
                var c = _contactService.TGetVisibleById(contactId, userId, canSeeAll);
                if (c != null) customers[c.ContactId] = $"{c.Name} {c.Surname}";
            }

            var agents = canSeeAll ? _propertyService.TGetAssignableAgents() : new();
            if (canSeeAll && existing != null && agents.All(a => a.Id != existing.Transaction.AgentUserId))
                agents.Insert(0, (existing.Transaction.AgentUserId, existing.AgentName + " (pasif)"));

            return new TransactionFormOptions
            {
                TransactionId = existing?.Transaction.TransactionId,
                PropertyId = property?.PropertyId ?? 0,
                PropertyTitle = property?.Title ?? "Gizli kayıt",
                Type = type,
                Customers = customers.OrderBy(x => x.Value, StringComparer.Create(PropertyLabels.Tr, true))
                    .Select(x => (x.Key, x.Value)).ToList(),
                CustomerHidden = existing?.ContactHidden ?? false,
                CanChooseAgent = canSeeAll,
                Agents = agents,
                ContractOriginalName = existing?.Transaction.ContractFileName == null ? null : existing.Transaction.ContractOriginalName,
                ContractUploadedAt = existing?.Transaction.ContractFileName == null ? null : existing.Transaction.ContractUploadedAt
            };
        }
    }
}
