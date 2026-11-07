using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.WebUI.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace EmlakOfisi.WebUI.Controllers
{
    // Portföyle ilgilenen müşteri ekleme / kaldırma (görev 12c). Sadece POST; listeler portföy ve müşteri detayında.
    // Yetki serviste: portföy ya da müşteri görünmüyorsa 404, kaldırma yetkisi yoksa 403.
    [Authorize(Roles = RoleNames.Staff)]
    public class PropertyInterestController : Controller
    {
        public const string ReturnToContact = "contact";

        private readonly IPropertyInterestService _interestService;

        public PropertyInterestController(IPropertyInterestService interestService)
        {
            _interestService = interestService;
        }

        // returnTo = "contact": müşteri sayfasından eklendi (müşteri sabit, portföy seçildi); aksi halde portföy sayfası
        [HttpPost]
        public IActionResult Add(int propertyId, int contactId, string? note, string? returnTo)
        {
            if (propertyId <= 0 || contactId <= 0)
            {
                TempData["Error"] = returnTo == ReturnToContact ? "Lütfen bir portföy seçin." : "Lütfen bir müşteri seçin.";
                return Back(returnTo, propertyId, contactId);
            }

            var result = _interestService.TAdd(propertyId, contactId, note, User.GetUserId(), User.CanSeeAll());
            if (result == OperationResult.NotFound)
                return NotFound();

            if (result == OperationResult.Invalid)
                TempData["Error"] = note?.Trim().Length > IPropertyInterestService.MaxNoteLength
                    ? $"Not en fazla {IPropertyInterestService.MaxNoteLength} karakter olabilir."
                    : "Bu kişi bu portföy için zaten ilgilenenler listesinde.";
            else
                TempData["Success"] = "İlgilenen kişi eklendi.";

            return Back(returnTo, propertyId, contactId);
        }

        [HttpPost]
        public IActionResult Delete(int id, string? returnTo)
        {
            // Geri dönülecek kayıtlar, satırın gerçekten bağlı olduğu portföy ve müşteri (formdan alınmaz)
            var result = _interestService.TDelete(id, User.GetUserId(), User.CanSeeAll(), out var propertyId, out var contactId);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            TempData["Success"] = "İlgilenen kişi listeden kaldırıldı.";
            return Back(returnTo, propertyId, contactId);
        }

        private IActionResult Back(string? returnTo, int propertyId, int contactId)
        {
            if (returnTo == ReturnToContact)
                return contactId > 0
                    ? RedirectToAction("Detail", "Contact", new { id = contactId }, "ilgilenilen-portfoyler")
                    : RedirectToAction("Index", "Contact");

            return propertyId > 0
                ? RedirectToAction("Detail", "Property", new { id = propertyId }, "ilgilenenler")
                : RedirectToAction("Index", "Property");
        }
    }
}
