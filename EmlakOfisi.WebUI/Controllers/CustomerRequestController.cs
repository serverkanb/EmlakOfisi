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
    // Müşteri talepleri (görev 17). Kurallar serviste (CustomerRequestManager).
    // Bağlı müşteriyi göremeyen 404, düzenleyemeyen 403; yetki form doğrulamasından ÖNCE.
    // "Başkasının müşterisine talep" izni admin tarafından verilir (Permissions.RequestEditAny).
    [Authorize(Roles = RoleNames.Staff)]
    public class CustomerRequestController : Controller
    {
        public const int PageSize = 20;

        private readonly ICustomerRequestService _requestService;
        private readonly IContactService _contactService;

        public CustomerRequestController(ICustomerRequestService requestService, IContactService contactService)
        {
            _requestService = requestService;
            _contactService = contactService;
        }

        private bool CanEditOthers => User.HasPermission(Permissions.RequestEditAny);

        // ---------------- Liste (menü) ----------------

        // status: "Open" (varsayılan), "Closed" ya da "all" (tümü)
        [HttpGet]
        public IActionResult Index(string? q, ListingType? listingType, PropertyType? propertyType,
            string? status = null, int page = 1)
        {
            RequestStatus? statusFilter = string.Equals(status, "all", StringComparison.OrdinalIgnoreCase) ? null
                : string.Equals(status, nameof(RequestStatus.Closed), StringComparison.OrdinalIgnoreCase) ? RequestStatus.Closed
                : RequestStatus.Open;
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            page = Math.Clamp(page, 1, 100_000);

            var filter = new CustomerRequestFilter { Search = q, ListingType = listingType, PropertyType = propertyType, Status = statusFilter };
            var (items, total) = _requestService.TGetVisibleList(userId, canSeeAll, filter, page, PageSize);

            var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            if (page > lastPage && total > 0)
                return RedirectToAction(nameof(Index), new { q, listingType, propertyType, status, page = lastPage });

            return View(new CustomerRequestListViewModel
            {
                // Düzenleme müşteri detayından yapılır; listede sadece görüntüleme ve "Uygun portföyler"
                Items = items.Select(r => CustomerRequestItemViewModel.From(r, canEdit: false)).ToList(),
                Q = q, ListingType = listingType, PropertyType = propertyType, Status = statusFilter,
                Page = page, PageSize = PageSize, TotalCount = total
            });
        }

        // ---------------- Ekleme ----------------

        [HttpGet]
        public IActionResult Create(int contactId)
        {
            var guard = Guard(contactId, out var contact);
            if (guard != null) return guard;

            ViewBag.ContactName = $"{contact!.Name} {contact.Surname}";
            ViewBag.ContactId = contactId;
            return View(new CustomerRequestFormViewModel { ListingType = contact.IsTenant && !contact.IsBuyer ? ListingType.Rent : ListingType.Sale });
        }

        [HttpPost]
        public IActionResult Create(int contactId, CustomerRequestFormViewModel model)
        {
            var guard = Guard(contactId, out var contact);
            if (guard != null) return guard;

            var request = ValidateAndMap(model);
            if (request == null)
                return FormView(model, contact!, null);

            var result = _requestService.TCreate(contactId, request, User.GetUserId(), User.CanSeeAll(), CanEditOthers);
            if (result == OperationResult.NotFound) return NotFound();
            if (result == OperationResult.Forbidden) return Forbid();
            if (result == OperationResult.Invalid)
            {
                ModelState.AddModelError("", "Talep bilgileri geçersiz.");
                return FormView(model, contact!, null);
            }

            TempData["Success"] = "Talep eklendi.";
            return BackToContact(contactId);
        }

        // ---------------- Düzenleme ----------------

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var request = _requestService.TGetVisibleById(id, User.GetUserId(), User.CanSeeAll());
            if (request == null) return NotFound();
            var guard = Guard(request.ContactId, out var contact);
            if (guard != null) return guard;

            return FormView(new CustomerRequestFormViewModel
            {
                ListingType = request.ListingType,
                PropertyType = request.PropertyType,
                City = request.City,
                District = request.District,
                RoomCount = request.RoomCount,
                MinArea = request.MinArea,
                MinPriceText = request.MinPrice?.ToString("0"),
                MaxPriceText = request.MaxPrice?.ToString("0"),
                Notes = request.Notes
            }, contact!, id);
        }

        [HttpPost]
        public IActionResult Edit(int id, CustomerRequestFormViewModel model)
        {
            var existing = _requestService.TGetVisibleById(id, User.GetUserId(), User.CanSeeAll());
            if (existing == null) return NotFound();
            var guard = Guard(existing.ContactId, out var contact);
            if (guard != null) return guard;

            var changes = ValidateAndMap(model);
            if (changes == null)
                return FormView(model, contact!, id);

            var result = _requestService.TUpdate(id, changes, User.GetUserId(), User.CanSeeAll(), CanEditOthers);
            if (result == OperationResult.NotFound) return NotFound();
            if (result == OperationResult.Forbidden) return Forbid();
            if (result == OperationResult.Invalid)
            {
                ModelState.AddModelError("", "Talep bilgileri geçersiz.");
                return FormView(model, contact!, id);
            }

            TempData["Success"] = "Talep güncellendi.";
            return BackToContact(existing.ContactId);
        }

        // ---------------- Kapat / yeniden aç ----------------

        [HttpPost]
        public IActionResult SetStatus(int id, RequestStatus status)
        {
            var result = _requestService.TSetStatus(id, status, User.GetUserId(), User.CanSeeAll(), CanEditOthers, out var contactId);
            if (result == OperationResult.NotFound) return NotFound();
            if (result == OperationResult.Forbidden) return Forbid();
            if (result == OperationResult.Invalid) return BadRequest();

            TempData["Success"] = status == RequestStatus.Closed ? "Talep kapatıldı." : "Talep yeniden açıldı.";
            return BackToContact(contactId);
        }

        // ---------------- Yardımcılar ----------------

        // Müşteri görünmüyorsa 404, talep ekleyip düzenleyemiyorsa 403 (form doğrulamasından önce)
        private IActionResult? Guard(int contactId, out Contact? contact)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            contact = _contactService.TGetVisibleById(contactId, userId, canSeeAll);
            if (contact == null)
                return NotFound();
            if (!_requestService.TCanEdit(contact, userId, canSeeAll, CanEditOthers))
                return Forbid();
            return null;
        }

        private ViewResult FormView(CustomerRequestFormViewModel model, Contact contact, int? requestId)
        {
            ViewBag.ContactName = $"{contact.Name} {contact.Surname}";
            ViewBag.ContactId = contact.ContactId;
            ViewBag.RequestId = requestId;
            return View(requestId == null ? nameof(Create) : nameof(Edit), model);
        }

        private CustomerRequest? ValidateAndMap(CustomerRequestFormViewModel m)
        {
            decimal? ParsePrice(string? text, string field)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                if (PropertyLabels.TryParsePrice(text, out var value)) return value;
                ModelState.AddModelError(field, "Fiyata sadece rakam girin (ör. 5000000).");
                return null;
            }

            var min = ParsePrice(m.MinPriceText, nameof(m.MinPriceText));
            var max = ParsePrice(m.MaxPriceText, nameof(m.MaxPriceText));
            if (min != null && max != null && min > max)
                ModelState.AddModelError(nameof(m.MaxPriceText), "En çok fiyat, en az fiyattan küçük olamaz.");
            if (!Enum.IsDefined(m.ListingType))
                ModelState.AddModelError(nameof(m.ListingType), "Geçersiz seçim.");
            if (m.PropertyType != null && !Enum.IsDefined(m.PropertyType.Value))
                ModelState.AddModelError(nameof(m.PropertyType), "Geçersiz tür.");

            if (!ModelState.IsValid)
                return null;

            return new CustomerRequest
            {
                ListingType = m.ListingType,
                PropertyType = m.PropertyType,
                City = Clean(m.City),
                District = Clean(m.District),
                RoomCount = m.PropertyType == PropertyType.Land ? null : Clean(m.RoomCount),
                MinArea = m.MinArea,
                MinPrice = min,
                MaxPrice = max,
                Notes = Clean(m.Notes)
            };
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private IActionResult BackToContact(int contactId) =>
            RedirectToAction("Detail", "Contact", new { id = contactId }, "talepler");
    }
}
