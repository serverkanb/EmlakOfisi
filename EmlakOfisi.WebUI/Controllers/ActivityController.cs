using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Müşteri detayındaki görüşme geçmişine ekleme / silme. Sadece POST; listeleme müşteri detayında.
    // Yetki serviste: müşteri görünmüyorsa 404, ekleme/silme yetkisi yoksa 403.
    [Authorize(Roles = RoleNames.Staff)]
    public class ActivityController : Controller
    {
        private static readonly DateTime MinDate = new(2000, 1, 1);

        private readonly IActivityService _activityService;

        public ActivityController(IActivityService activityService)
        {
            _activityService = activityService;
        }

        [HttpPost]
        public IActionResult Create(int contactId, ActivityFormViewModel model)
        {
            // Önce yetki (form hatalarından önce): göremeyen 404, ekleyemeyen 403 alır
            var check = _activityService.TCheckAdd(contactId, User.GetUserId(), User.CanSeeAll());
            if (check == OperationResult.NotFound)
                return NotFound();
            if (check == OperationResult.Forbidden)
                return Forbid();

            // Tarih makul aralıkta olmalı (ileri tarihli not geçmişin en üstüne yapışmasın)
            if (model.ActivityDate is DateTime date && (date < MinDate || date > DateTime.Now.AddDays(1)))
                ModelState.AddModelError(nameof(model.ActivityDate), "Tarih 2000 ile yarın arasında olmalıdır.");

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
                return RedirectToAction("Detail", "Contact", new { id = contactId });
            }

            var activity = new Activity
            {
                Type = model.Type,
                Description = model.Description.Trim(),
                ActivityDate = model.ActivityDate ?? DateTime.Now
            };

            var result = _activityService.TAdd(contactId, activity, User.GetUserId(), User.CanSeeAll());
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            TempData["Success"] = "Görüşme kaydedildi.";
            return RedirectToAction("Detail", "Contact", new { id = contactId });
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            // Geri dönülecek müşteri, notun gerçekten bağlı olduğu müşteri (formdan alınmaz)
            var result = _activityService.TDelete(id, User.GetUserId(), User.CanSeeAll(), out var contactId);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            TempData["Success"] = "Görüşme silindi.";
            return RedirectToAction("Detail", "Contact", new { id = contactId });
        }
    }
}
