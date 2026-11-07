using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Ana sayfa (görev 15). Sayılar sadece görünürlük filtreli sorgulardan gelir:
    // danışman kendi + herkese açık kayıtları, yönetici hepsini sayar. Randevular görev 14'te eklenecek.
    [Authorize(Roles = RoleNames.Staff)]
    public class DashboardController : Controller
    {
        private readonly IPropertyService _propertyService;
        private readonly IContactService _contactService;

        public DashboardController(IPropertyService propertyService, IContactService contactService)
        {
            _propertyService = propertyService;
            _contactService = contactService;
        }

        public IActionResult Index()
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            var propertyCounts = _propertyService.TGetVisibleCounts(userId, canSeeAll);

            return View(new DashboardViewModel
            {
                ActiveProperties = propertyCounts.Active,
                ActiveSale = propertyCounts.ActiveSale,
                ActiveRent = propertyCounts.ActiveRent,
                PassiveProperties = propertyCounts.Passive,
                SoldProperties = propertyCounts.Sold,
                RentedProperties = propertyCounts.Rented,
                Contacts = _contactService.TCountVisible(userId, canSeeAll),
                CanSeeAll = canSeeAll
            });
        }
    }
}
