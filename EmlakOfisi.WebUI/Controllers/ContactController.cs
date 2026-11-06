using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Müşteriler. Sadece ofis çalışanları (rolü olan kullanıcılar).
    // Görünürlük kuralı serviste/sorguda uygulanır; controller sadece giriş yapan kişiyi iletir.
    // Görmeye yetkisi olmayan kayıt -> 404 (varlığı belli edilmez); görüp düzenleyemediği kayıt -> 403.
    // Silme gerçek silme değildir (IsDeleted). Tüm POST'larda antiforgery global olarak açık.
    [Authorize(Roles = RoleNames.Staff)]
    public class ContactController : Controller
    {
        public const int PageSize = 20;

        private readonly IContactService _contactService;
        private readonly IActivityService _activityService;

        public ContactController(IContactService contactService, IActivityService activityService)
        {
            _contactService = contactService;
            _activityService = activityService;
        }

        [HttpGet]
        public IActionResult Index(string? q, ContactRole? role, int page = 1)
        {
            var userId = User.GetUserId();
            var (items, total) = _contactService.TGetVisibleList(userId, User.CanSeeAll(), q, role, page, PageSize);

            // Olmayan bir sayfa istenirse (ör. ?page=99) son sayfaya git
            var lastPage = (int)Math.Ceiling(total / (double)PageSize);
            if (items.Count == 0 && page > 1 && lastPage >= 1)
                return RedirectToAction(nameof(Index), new { q, role, page = lastPage });

            var model = new ContactListViewModel
            {
                Items = items.Select(c => ToListItem(c, userId)).ToList(),
                Search = q,
                Role = role,
                Page = page < 1 ? 1 : page,
                PageSize = PageSize,
                TotalCount = total
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Detail(int id)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var contact = _contactService.TGetVisibleById(id, userId, canSeeAll);
            if (contact == null)
                return NotFound(); // görmeye yetkisi yoksa kaydın varlığı da belli edilmez

            var model = new ContactDetailViewModel
            {
                Summary = ToListItem(contact, userId),
                Phone2 = contact.Phone2,
                Email = contact.Email,
                Notes = contact.Notes,
                CreatedAt = contact.CreatedAt,
                UpdatedAt = contact.UpdatedAt,
                CanEdit = _contactService.TCanEdit(contact, userId, canSeeAll),
                CanDelete = _contactService.TCanDelete(contact, userId, canSeeAll),
                CanAddActivity = _activityService.TCanAdd(contact, userId, canSeeAll),
                Activities = (_activityService.TGetForContact(id, userId, canSeeAll) ?? new List<Activity>())
                    .Select(a => new ActivityItemViewModel
                    {
                        ActivityId = a.ActivityId,
                        Type = a.Type,
                        Description = a.Description,
                        ActivityDate = a.ActivityDate,
                        UserName = a.User == null ? "" : $"{a.User.Name} {a.User.Surname}",
                        CanDelete = _activityService.TCanDelete(a, userId, canSeeAll)
                    })
                    .ToList()
            };

            return View(model);
        }

        // ---------------- Ekleme ----------------

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ContactFormViewModel()); // görünürlük varsayılanı: Sadece Ben
        }

        [HttpPost]
        public IActionResult Create(ContactFormViewModel model)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            ValidateForm(model, userId, canSeeAll, excludeContactId: null);
            if (!ModelState.IsValid)
                return View(model);

            var contact = ToEntity(model);
            _contactService.TCreate(contact, userId); // sahip = giriş yapan kişi (formdan gelmez)

            TempData["Success"] = $"{contact.Name} {contact.Surname} eklendi.";
            return RedirectToAction(nameof(Detail), new { id = contact.ContactId });
        }

        // ---------------- Düzenleme ----------------

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var contact = _contactService.TGetVisibleById(id, userId, canSeeAll);
            if (contact == null)
                return NotFound();
            if (!_contactService.TCanEdit(contact, userId, canSeeAll))
                return Forbid();

            ViewBag.ContactId = id;
            return View(new ContactFormViewModel
            {
                Name = contact.Name,
                Surname = contact.Surname,
                Phone = contact.Phone,
                Phone2 = contact.Phone2,
                Email = contact.Email,
                Notes = contact.Notes,
                IsBuyer = contact.IsBuyer,
                IsSeller = contact.IsSeller,
                IsTenant = contact.IsTenant,
                IsLandlord = contact.IsLandlord,
                Visibility = contact.Visibility
            });
        }

        [HttpPost]
        public IActionResult Edit(int id, ContactFormViewModel model)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            ViewBag.ContactId = id;

            // Önce yetki: göremiyorsa 404, düzenleyemiyorsa 403 (form hatalarından önce, bilgi sızmasın)
            var existing = _contactService.TGetVisibleById(id, userId, canSeeAll);
            if (existing == null)
                return NotFound();
            if (!_contactService.TCanEdit(existing, userId, canSeeAll))
                return Forbid();

            ValidateForm(model, userId, canSeeAll, excludeContactId: id);
            if (!ModelState.IsValid)
                return View(model);

            // Servis yetkiyi tekrar kontrol eder ve sadece izinli alanları kopyalar
            var result = _contactService.TUpdate(id, ToEntity(model), userId, canSeeAll);
            if (result == ContactOperationResult.NotFound)
                return NotFound();
            if (result == ContactOperationResult.Forbidden)
                return Forbid();

            TempData["Success"] = "Müşteri bilgileri güncellendi.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // ---------------- Silme ----------------

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var result = _contactService.TSoftDelete(id, User.GetUserId(), User.CanSeeAll());
            if (result == ContactOperationResult.NotFound)
                return NotFound();
            if (result == ContactOperationResult.Forbidden)
                return Forbid();

            TempData["Success"] = "Müşteri silindi.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- Yardımcılar ----------------

        // Telefon geçerliliği ve tekrar kayıt kontrolü (sadece POST'ta; ayrı bir "numara var mı" ucu yok)
        private void ValidateForm(ContactFormViewModel model, int userId, bool canSeeAll, int? excludeContactId)
        {
            AddPhoneError(nameof(model.Phone), model.Phone);
            AddPhoneError(nameof(model.Phone2), model.Phone2);

            if (!Enum.IsDefined(model.Visibility))
                ModelState.AddModelError(nameof(model.Visibility), "Geçersiz görünürlük seçimi.");

            if (!ModelState.IsValid)
                return;

            var duplicate = _contactService.TCheckDuplicatePhone(model.Phone, userId, canSeeAll, excludeContactId);
            if (!duplicate.Exists)
                return;

            if (duplicate.VisibleContactId != null)
            {
                // Kullanıcı o kaydı görebiliyor: formun üstünde kayda link verilir, alanda kısa uyarı
                ViewBag.DuplicateContactId = duplicate.VisibleContactId;
                ModelState.AddModelError(nameof(model.Phone), "Bu numara kayıtlı.");
            }
            else
            {
                // Göremediği bir kayıt: kim olduğu, kimin kaydı olduğu söylenmez
                ModelState.AddModelError(nameof(model.Phone),
                    "Bu telefon numarası ofiste başka bir kayıtta bulunuyor. Lütfen ofis yöneticinize danışın.");
            }
        }

        // Telefon sadece rakam içermeli ve geçerli uzunlukta olmalı (boşsa zorunluluk ayrıca kontrol edilir)
        private void AddPhoneError(string field, string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return;

            if (!PhoneNormalizer.HasOnlyDigits(phone))
                ModelState.AddModelError(field, "Telefona sadece rakam girilebilir.");
            else if (!PhoneNormalizer.IsValid(phone))
                ModelState.AddModelError(field, "Geçerli bir telefon girin (ör. 05321112233).");
        }

        private static Contact ToEntity(ContactFormViewModel m) => new()
        {
            Name = m.Name.Trim(),
            Surname = m.Surname.Trim(),
            Phone = m.Phone.Trim(),
            Phone2 = string.IsNullOrWhiteSpace(m.Phone2) ? null : m.Phone2.Trim(),
            Email = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email.Trim(),
            Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes.Trim(),
            IsBuyer = m.IsBuyer,
            IsSeller = m.IsSeller,
            IsTenant = m.IsTenant,
            IsLandlord = m.IsLandlord,
            Visibility = m.Visibility
        };

        private static ContactListItemViewModel ToListItem(Contact c, int userId)
        {
            // Linkler sadece rakamlardan (normalize edilmiş 10 hane) kurulur; geçersizse buton gösterilmez
            var valid = c.PhoneNormalized.Length == 10 && c.PhoneNormalized.All(char.IsAsciiDigit);
            return new ContactListItemViewModel
            {
                ContactId = c.ContactId,
                FullName = $"{c.Name} {c.Surname}",
                Phone = c.Phone,
                TelLink = valid ? $"tel:+90{c.PhoneNormalized}" : string.Empty,
                WhatsAppLink = valid ? $"https://wa.me/{PhoneNormalizer.ToWhatsApp(c.PhoneNormalized)}" : string.Empty,
                IsBuyer = c.IsBuyer,
                IsSeller = c.IsSeller,
                IsTenant = c.IsTenant,
                IsLandlord = c.IsLandlord,
                Visibility = c.Visibility,
                IsMine = c.CreatedByUserId == userId
            };
        }
    }
}
