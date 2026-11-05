using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // InstrumentTrade'deki LoginController ile aynı akış. Farklar:
    // - Pasif kullanıcı kontrolü giriş YAPILMADAN önce yapılır (önce giriş yapıp sonra kontrol etmek cookie bırakıyordu)
    //   ve pasif hesapta da hatalı deneme sayılır / kilit uygulanır.
    // - returnUrl sadece site içi adreslere yönlendirir (dış siteye yönlendirme açığı yok).
    // - Hatalı denemelerde hesap geçici kilitlenir.
    // - Çıkış POST ile yapılır (başka bir siteden link ile çıkış yaptırılamaz).
    [AllowAnonymous]
    public class LoginController : Controller
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;

        public LoginController(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Index(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToLocal(returnUrl);

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByNameAsync(model.UserName);

            if (user != null)
            {
                // 1) Şifre + kilit kontrolü (cookie bırakmaz, hatalı denemeyi sayar; pasif hesap için de geçerli)
                var check = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

                if (check.IsLockedOut)
                {
                    ModelState.AddModelError("", "Çok sayıda hatalı deneme yapıldı. Lütfen 15 dakika sonra tekrar deneyin.");
                    return View(model);
                }

                if (check.Succeeded)
                {
                    // 2) Aktiflik kontrolü
                    if (!user.IsActive)
                    {
                        ModelState.AddModelError("", "Hesabınız pasif durumda. Lütfen yöneticiyle iletişime geçin.");
                        return View(model);
                    }

                    // 3) Giriş
                    await _signInManager.SignInAsync(user, model.RememberMe);
                    return RedirectToLocal(returnUrl);
                }
            }

            ModelState.AddModelError("", "Kullanıcı adı veya şifre yanlış.");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }
    }
}
