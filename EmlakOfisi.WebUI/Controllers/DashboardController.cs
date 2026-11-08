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
        private readonly ITransactionService _transactionService;

        public DashboardController(IPropertyService propertyService, IContactService contactService,
            IAppointmentService appointmentService, ITransactionService transactionService)
        {
            _transactionService = transactionService;
            _appointmentService = appointmentService;
            _propertyService = propertyService;
            _contactService = contactService;
        }

        public IActionResult Index()
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            var propertyCounts = _propertyService.TGetVisibleCounts(userId, canSeeAll);
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var month = _transactionService.TGetCompletedSummary(userId, canSeeAll, monthStart, monthStart.AddMonths(1));
            var (upcoming, upcomingCount) = _appointmentService.TGetVisibleList(userId, canSeeAll,
                new AppointmentFilter { Scope = AppointmentScope.Upcoming }, 1, UpcomingShown);
            var (leases, leaseCount) = _transactionService.TGetVisibleList(userId, canSeeAll,
                new TransactionFilter { Scope = TransactionScope.LeaseEnding }, 1, UpcomingShown);

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
                MonthSaleCount = month.SaleCount,
                MonthRentCount = month.RentCount,
                MonthCommission = month.Commission,
                MonthAgentShare = canSeeAll ? month.AgentShare : month.MyAgentShare,
                Upcoming = upcoming.Select(r => AppointmentItemViewModel.From(r, userId, false)).ToList(),
                LeaseEndingCount = leaseCount,
                LeaseEnding = leases.Select(r => TransactionItemViewModel.From(r, userId, false, canSeeAll)).ToList()
            });
        }
    }
}
