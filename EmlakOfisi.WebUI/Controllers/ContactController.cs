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
    [Authorize(Roles = RoleNames.Staff)]
    public class ContactController : Controller
    {
        public const int PageSize = 20;

        private readonly IContactService _contactService;

        public ContactController(IContactService contactService)
        {
            _contactService = contactService;
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
                CanDelete = _contactService.TCanDelete(contact, userId, canSeeAll)
            };

            return View(model);
        }

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
