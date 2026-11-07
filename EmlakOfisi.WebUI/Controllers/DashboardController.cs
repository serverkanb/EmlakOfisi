using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Ana sayfa (görev 15). Sayılar sadece görünürlük filtreli sorgulardan gelir:
    // danışman kendi + herkese açık kayıtları, yönetici hepsini sayar. Yaklaşan randevular da aynı kuralla (görev 14).
    [Authorize(Roles = RoleNames.Staff)]
    public class DashboardController : Controller
    {
        public const int UpcomingShown = 5;

        private readonly IPropertyService _propertyService;
        private readonly IContactService _contactService;
        private readonly IAppointmentService _appointmentService;

        public DashboardController(IPropertyService propertyService, IContactService contactService,
            IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
            _propertyService = propertyService;
            _contactService = contactService;
        }

        public IActionResult Index()
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            var propertyCounts = _propertyService.TGetVisibleCounts(userId, canSeeAll);
            var (upcoming, upcomingCount) = _appointmentService.TGetVisibleList(userId, canSeeAll,
                new AppointmentFilter { Scope = AppointmentScope.Upcoming }, 1, UpcomingShown);

            return View(new DashboardViewModel
            {
                ActiveProperties = propertyCounts.Active,
                ActiveSale = propertyCounts.ActiveSale,
                ActiveRent = propertyCounts.ActiveRent,
                PassiveProperties = propertyCounts.Passive,
                SoldProperties = propertyCounts.Sold,
                RentedProperties = propertyCounts.Rented,
                Contacts = _contactService.TCountVisible(userId, canSeeAll),
                CanSeeAll = canSeeAll,
                UpcomingCount = upcomingCount,
                Upcoming = upcoming.Select(r => AppointmentItemViewModel.From(r, userId, false)).ToList()
            });
        }
    }
}
