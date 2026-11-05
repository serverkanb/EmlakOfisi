using System.Globalization;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.ViewComponents.Layout
{
    // Üst menüdeki kullanıcı kutusu ve çıkış (InstrumentTrade'deki AdminLayoutNavbarViewComponent karşılığı)
    public class LayoutNavbarViewComponent : ViewComponent
    {
        private static readonly CultureInfo TurkishCulture = new("tr-TR");

        private readonly UserManager<AppUser> _userManager;

        public LayoutNavbarViewComponent(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(UserClaimsPrincipal);

            var model = new LayoutUserViewModel
            {
                FullName = user == null ? (User.Identity?.Name ?? "") : $"{user.Name} {user.Surname}",
                RoleLabel = GetRoleLabel(),
                Initials = user == null ? "?" : $"{FirstChar(user.Name)}{FirstChar(user.Surname)}"
            };

            return View(model);
        }

        private string GetRoleLabel()
        {
            if (UserClaimsPrincipal.IsInRole(RoleNames.Admin)) return "Sistem yöneticisi";
            if (UserClaimsPrincipal.IsInRole(RoleNames.Manager)) return "Ofis yöneticisi";
            if (UserClaimsPrincipal.IsInRole(RoleNames.Agent)) return "Danışman";
            return "";
        }

        private static string FirstChar(string value)
        {
            // Türkçe büyük harf: "i" -> "İ" (sunucu kültüründen bağımsız)
            return string.IsNullOrEmpty(value) ? "" : value[..1].ToUpper(TurkishCulture);
        }
    }
}
