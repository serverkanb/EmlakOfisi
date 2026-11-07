using System.Globalization;
using System.Text.RegularExpressions;
using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Extensions;
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

        public TransactionController(ITransactionService transactionService, IPropertyService propertyService,
            IContactService contactService)
        {
            _transactionService = transactionService;
            _propertyService = propertyService;
            _contactService = contactService;
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
                    _transactionService.TCanEdit(r.Transaction, userId, canSeeAll))).ToList(),
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

            var transaction = ValidateAndMap(model, requireCustomer: true);
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
                CommissionRateText = t.CommissionRate?.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ','),
                CommissionAmountText = t.CommissionAmount?.ToString("0"),
                AgentUserId = t.AgentUserId,
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

            var changes = ValidateAndMap(model, requireCustomer: !row.ContactHidden);
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

        private Transaction? ValidateAndMap(TransactionFormViewModel m, bool requireCustomer)
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

            decimal? rate = null;
            if (!string.IsNullOrWhiteSpace(m.CommissionRateText))
            {
                var text = m.CommissionRateText.Trim().Replace(',', '.');
                if (Regex.IsMatch(text, @"^\d{1,3}(\.\d{1,2})?$") &&
                    decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var r) && r <= 100)
                    rate = r;
                else
                    ModelState.AddModelError(nameof(m.CommissionRateText), "Oranı 0 ile 100 arasında girin (ör. 2 ya da 2,5).");
            }

            if (requireCustomer && m.CustomerContactId <= 0)
                ModelState.AddModelError(nameof(m.CustomerContactId), "Müşteri seçin.");
            if (!Enum.IsDefined(m.Status))
                ModelState.AddModelError(nameof(m.Status), "Geçersiz durum.");

            if (!ModelState.IsValid)
                return null;

            return new Transaction
            {
                CustomerContactId = m.CustomerContactId,
                Status = m.Status,
                OfferPrice = offer,
                AgreedPrice = agreed,
                CommissionRate = rate,
                CommissionAmount = amount,
                AgentUserId = m.AgentUserId,
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
                Agents = agents
            };
        }
    }
}
