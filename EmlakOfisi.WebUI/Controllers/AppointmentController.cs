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
    // Randevular (görev 14). Kurallar serviste (AppointmentManager).
    // Göremeyen 404, düzenleyemeyen 403; yetki form doğrulamasından ÖNCE. Silme yok, "İptal" var.
    [Authorize(Roles = RoleNames.Staff)]
    public class AppointmentController : Controller
    {
        public const int PageSize = 20;
        public const int MaxChoices = 100;

        private readonly IAppointmentService _appointmentService;
        private readonly IContactService _contactService;
        private readonly IPropertyService _propertyService;

        public AppointmentController(IAppointmentService appointmentService, IContactService contactService,
            IPropertyService propertyService)
        {
            _appointmentService = appointmentService;
            _contactService = contactService;
            _propertyService = propertyService;
        }

        // ---------------- Liste ----------------

        [HttpGet]
        public IActionResult Index(AppointmentScope scope = AppointmentScope.Upcoming, bool mine = false, int page = 1)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            if (!Enum.IsDefined(scope)) scope = AppointmentScope.Upcoming;
            page = Math.Clamp(page, 1, 100_000);

            var filter = new AppointmentFilter { Scope = scope, OnlyMine = mine };
            var (items, total) = _appointmentService.TGetVisibleList(userId, canSeeAll, filter, page, PageSize);

            var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            if (page > lastPage && total > 0)
                return RedirectToAction(nameof(Index), new { scope, mine, page = lastPage });

            return View(new AppointmentListViewModel
            {
                Items = items.Select(r => AppointmentItemViewModel.From(r, userId,
                    _appointmentService.TCanEdit(r.Appointment, userId, canSeeAll))).ToList(),
                Scope = scope,
                Mine = mine,
                CanSeeAll = canSeeAll,
                Page = page,
                PageSize = PageSize,
                TotalCount = total
            });
        }

        // ---------------- Ekleme ----------------

        // Müşteri veya portföy detayından ilgili kayıt seçili açılabilir. Görünmeyen id sessizce yok sayılır.
        [HttpGet]
        public IActionResult Create(int? contactId, int? propertyId, AppointmentType? type)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var model = new AppointmentFormViewModel
            {
                Type = type != null && Enum.IsDefined(type.Value) ? type.Value : AppointmentType.Meeting,
                StartAt = DateTime.Today.AddDays(1).AddHours(10),
                AgentUserId = userId
            };

            if (contactId != null && _contactService.TGetVisibleById(contactId.Value, userId, canSeeAll) != null)
                model.ContactId = contactId;

            if (propertyId != null)
            {
                // Adres "Yer" alanına KOPYALANMAZ: yönetici sorumluyu başka danışmana verirse portföy ona gizli olabilir;
                // kartta adres/harita linki her görüntüleyen için portföy görünürlüğünden canlı üretilir.
                if (_propertyService.TGetVisibleById(propertyId.Value, userId, canSeeAll) != null)
                    model.PropertyId = propertyId;
            }

            ViewBag.Options = BuildOptions(null, null, model.ContactId, model.PropertyId);
            return View(model);
        }

        [HttpPost]
        public IActionResult Create(AppointmentFormViewModel model)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var appointment = ValidateAndMap(model);
            if (appointment == null)
            {
                ViewBag.Options = BuildOptions(null, null, model.ContactId, model.PropertyId);
                return View(model);
            }

            var result = _appointmentService.TCreate(appointment, userId, canSeeAll);
            if (result != OperationResult.Success)
            {
                ModelState.AddModelError("", "Seçilen müşteri, portföy veya danışman geçersiz.");
                ViewBag.Options = BuildOptions(null, null, model.ContactId, model.PropertyId);
                return View(model);
            }

            TempData["Success"] = "Randevu eklendi.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- Düzenleme ----------------

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var row = _appointmentService.TGetVisibleById(id, userId, canSeeAll);
            if (row == null)
                return NotFound();
            if (!_appointmentService.TCanEdit(row.Appointment, userId, canSeeAll))
                return Forbid();

            var a = row.Appointment;
            ViewBag.Options = BuildOptions(id, row, a.ContactId, a.PropertyId);
            return View(new AppointmentFormViewModel
            {
                Type = a.Type,
                Status = a.Status,
                StartAt = a.StartAt,
                EndAt = a.EndAt,
                // Gizli bağlantının id'si forma konmaz (serviste korunur)
                ContactId = row.ContactHidden ? null : a.ContactId,
                PropertyId = row.PropertyHidden ? null : a.PropertyId,
                AgentUserId = a.AgentUserId,
                Location = a.Location,
                Notes = a.Notes
            });
        }

        [HttpPost]
        public IActionResult Edit(int id, AppointmentFormViewModel model)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            // Önce yetki (form hatalarından önce): göremeyen 404, düzenleyemeyen 403
            var row = _appointmentService.TGetVisibleById(id, userId, canSeeAll);
            if (row == null)
                return NotFound();
            if (!_appointmentService.TCanEdit(row.Appointment, userId, canSeeAll))
                return Forbid();

            var changes = ValidateAndMap(model);
            if (changes == null)
            {
                ViewBag.Options = BuildOptions(id, row, model.ContactId, model.PropertyId);
                return View(model);
            }

            var result = _appointmentService.TUpdate(id, changes, userId, canSeeAll);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();
            if (result == OperationResult.Invalid)
            {
                ModelState.AddModelError("", "Seçilen müşteri, portföy veya danışman geçersiz.");
                ViewBag.Options = BuildOptions(id, row, model.ContactId, model.PropertyId);
                return View(model);
            }

            TempData["Success"] = "Randevu güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- Durum (Yapıldı / İptal / tekrar planla) ----------------

        [HttpPost]
        public IActionResult SetStatus(int id, AppointmentStatus status, AppointmentScope scope = AppointmentScope.Upcoming, bool mine = false)
        {
            var result = _appointmentService.TSetStatus(id, status, User.GetUserId(), User.CanSeeAll());
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            if (result == OperationResult.Invalid)
                TempData["Error"] = "Geçersiz durum.";
            else
                TempData["Success"] = status == AppointmentStatus.Cancelled ? "Randevu iptal edildi."
                    : status == AppointmentStatus.Done ? "Randevu yapıldı olarak işaretlendi." : "Randevu yeniden planlandı.";

            return RedirectToAction(nameof(Index), new { scope = Enum.IsDefined(scope) ? scope : AppointmentScope.Upcoming, mine });
        }

        // ---------------- Yardımcılar ----------------

        private Appointment? ValidateAndMap(AppointmentFormViewModel m)
        {
            var earliest = new DateTime(2000, 1, 1);
            var latest = DateTime.Today.AddYears(2);

            if (m.StartAt != null && (m.StartAt < earliest || m.StartAt > latest))
                ModelState.AddModelError(nameof(m.StartAt), "Başlangıç tarihi 2000 ile önümüzdeki 2 yıl arasında olmalıdır.");
            if (m.StartAt != null && m.EndAt != null && m.EndAt <= m.StartAt)
                ModelState.AddModelError(nameof(m.EndAt), "Bitiş, başlangıçtan sonra olmalıdır.");
            if (!Enum.IsDefined(m.Type)) ModelState.AddModelError(nameof(m.Type), "Geçersiz tür.");
            if (!Enum.IsDefined(m.Status)) ModelState.AddModelError(nameof(m.Status), "Geçersiz durum.");

            if (!ModelState.IsValid)
                return null;

            return new Appointment
            {
                Type = m.Type,
                Status = m.Status,
                StartAt = m.StartAt!.Value,
                EndAt = m.EndAt,
                ContactId = m.ContactId,
                PropertyId = m.PropertyId,
                AgentUserId = m.AgentUserId,
                Location = Clean(m.Location),
                Notes = Clean(m.Notes)
            };
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // Seçenekler sadece kullanıcının GÖREBİLDİĞİ kayıtlardan (filtresiz arama ucu yok)
        private AppointmentFormOptions BuildOptions(int? appointmentId, AppointmentRow? existing, int? contactId, int? propertyId)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var contacts = _contactService.TGetVisibleList(userId, canSeeAll, null, null, 1, MaxChoices).Items
                .ToDictionary(c => c.ContactId, c => $"{c.Name} {c.Surname}");
            if (contactId != null && !contacts.ContainsKey(contactId.Value))
            {
                var c = _contactService.TGetVisibleById(contactId.Value, userId, canSeeAll);
                if (c != null) contacts[c.ContactId] = $"{c.Name} {c.Surname}";
            }

            var properties = _propertyService.TGetVisibleList(userId, canSeeAll,
                    new PropertyFilter { Status = PropertyStatus.Active }, 1, MaxChoices).Items
                .ToDictionary(p => p.PropertyId, p => p.Title);
            if (propertyId != null && !properties.ContainsKey(propertyId.Value))
            {
                // Pasif/satılmış ama görünür bir portföy (düzenlemede mevcut bağlantı) listede kalsın
                var p = _propertyService.TGetVisibleById(propertyId.Value, userId, canSeeAll);
                if (p != null) properties[p.PropertyId] = p.Title;
            }

            var agents = canSeeAll ? _propertyService.TGetAssignableAgents() : new();
            // Mevcut sorumlu pasifse yine seçili kalsın (aksi halde tarayıcı ilk kişiyi gönderir)
            if (canSeeAll && existing != null && agents.All(a => a.Id != existing.Appointment.AgentUserId))
                agents.Insert(0, (existing.Appointment.AgentUserId, existing.AgentName + " (pasif)"));

            var comparer = StringComparer.Create(PropertyLabels.Tr, true);
            return new AppointmentFormOptions
            {
                AppointmentId = appointmentId,
                Contacts = contacts.OrderBy(x => x.Value, comparer).Select(x => (x.Key, x.Value)).ToList(),
                Properties = properties.OrderBy(x => x.Value, comparer).Select(x => (x.Key, x.Value)).ToList(),
                ContactHidden = existing?.ContactHidden ?? false,
                PropertyHidden = existing?.PropertyHidden ?? false,
                CanChooseAgent = canSeeAll,
                Agents = agents
            };
        }
    }
}
