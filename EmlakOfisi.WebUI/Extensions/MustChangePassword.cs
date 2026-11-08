using System.Security.Claims;
using EmlakOfisi.EntityLayer.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace EmlakOfisi.WebUI.Extensions
{
    // İlk girişte şifre değiştirme zorunluluğu (ilk admin ve admin'in açtığı hesaplar).
    // Giriş yapınca oturuma "şifre değiştirmeli" işareti konur; işaret varken her sayfa şifre değiştirme sayfasına
    // yönlenir (şifre değiştirme, çıkış, giriş ve hata sayfaları hariç). Şifre değişince işaret kalkar ve oturum yenilenir.
    public static class MustChangePassword
    {
        public const string ClaimType = "must_change_password";

        public static bool MustChange(this ClaimsPrincipal user) => user.HasClaim(ClaimType, "true");
    }

    // Oturum oluşturulurken (giriş, oturum yenileme) kullanıcının işaretini claim olarak ekler
    public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, AppRole>
    {
        public AppClaimsPrincipalFactory(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager,
            IOptions<IdentityOptions> options) : base(userManager, roleManager, options)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);
            if (user.MustChangePassword)
                identity.AddClaim(new Claim(MustChangePassword.ClaimType, "true"));
            return identity;
        }
    }

    // Global filtre: işaretli kullanıcıyı şifre değiştirme sayfasına yönlendirir
    public class MustChangePasswordFilter : IActionFilter
    {
        private static readonly HashSet<string> AllowedControllers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Login", "Error", "Manifest"
        };

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true || !context.HttpContext.User.MustChange())
                return;
            if (context.ActionDescriptor is not ControllerActionDescriptor action)
                return;
            if (AllowedControllers.Contains(action.ControllerName))
                return;
            if (action.ControllerName == "Profile" && action.ActionName == "ChangePassword")
                return;

            context.Result = new RedirectToActionResult("ChangePassword", "Profile", null);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
