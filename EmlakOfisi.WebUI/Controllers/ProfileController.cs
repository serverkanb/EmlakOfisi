using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Giriş yapan herkesin kendi şifresini değiştirmesi
    // (InstrumentTrade'deki Areas/Customer/ProfileController.ChangePassword karşılığı).
    public class ProfileController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public ProfileController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            ViewBag.MustChange = User.MustChange();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            ViewBag.MustChange = User.MustChange();
            if (!ModelState.IsValid)
                return View(model);

            // Yeni şifre eskisiyle aynı olamaz (admin'in bildiği şifre tekrar girilirse zorunluluğun anlamı kalmaz)
            if (model.NewPassword == model.CurrentPassword)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Yeni şifre mevcut şifreden farklı olmalıdır.");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            // İlk giriş zorunluluğu şifreyle aynı kayıtta kalkar (ChangePasswordAsync kullanıcıyı tek seferde kaydeder;
            // şifre değişmezse bu değer de kaydedilmez)
            var wasForced = user.MustChangePassword;
            user.MustChangePassword = false;

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                user.MustChangePassword = wasForced;
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            // Şifre değişince security stamp değişir; mevcut oturum yenilenmezse kullanıcı birkaç dakika sonra çıkış yapar.
            // Yenilenen oturumda "şifre değiştirmeli" işareti de artık yok.
            await _signInManager.RefreshSignInAsync(user);

            TempData["Success"] = "Şifreniz değiştirildi.";
            return RedirectToAction("Index", "Dashboard");
        }
    }
}
