using System.Security.Claims;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Kişi bazlı ek izinler (görev 17). Sadece Admin. RoleAssignController ile aynı akış:
    // kullanıcı id'si adresten gelir, sadece tanımlı izinler (Permissions.All) verilir,
    // değişiklik oturuma yansısın diye security stamp güncellenir (başkasının oturumu en geç 5 dk içinde yenilenir).
    [Authorize(Roles = RoleNames.Admin)]
    public class UserPermissionController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public UserPermissionController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            var granted = (await _userManager.GetClaimsAsync(user))
                .Where(c => c.Type == Permissions.ClaimType).Select(c => c.Value).ToHashSet();
            var roles = await _userManager.GetRolesAsync(user);

            return View(new UserPermissionPageViewModel
            {
                UserId = user.Id,
                FullName = $"{user.Name} {user.Surname}",
                CanSeeAll = roles.Contains(RoleNames.Admin) || roles.Contains(RoleNames.Manager),
                CommissionShareText = PropertyLabels.PercentText(user.CommissionSharePercent),
                Permissions = Permissions.All.Select(p => new UserPermissionItemViewModel
                {
                    Name = p,
                    Label = Permissions.Label(p),
                    Description = Permissions.Description(p),
                    Granted = granted.Contains(p)
                }).ToList()
            });
        }

        // permissions: işaretli izin adları (tanımlı olmayanlar yok sayılır).
        // commissionShare: danışman prim oranı (%), boşsa kaldırılır. Geçersizse hiçbir şey kaydedilmez.
        [HttpPost]
        public async Task<IActionResult> Edit(int id, List<string>? permissions, string? commissionShare = null)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            decimal? share = null;
            if (!string.IsNullOrWhiteSpace(commissionShare))
            {
                if (!PropertyLabels.TryParsePercent(commissionShare, out var parsed))
                {
                    TempData["Error"] = "Prim oranını 0 ile 100 arasında girin (ör. 40 ya da 37,5). Değişiklik yapılmadı.";
                    return RedirectToAction(nameof(Edit), new { id });
                }
                share = parsed;
            }

            // Prim oranı: sadece yeni tamamlanacak işlemleri etkiler (tamamlanmış işlemlerde oran kayıtlı)
            var shareFailed = false;
            if (user.CommissionSharePercent != share)
            {
                user.CommissionSharePercent = share;
                shareFailed = !(await _userManager.UpdateAsync(user)).Succeeded;
            }

            var wanted = (permissions ?? new()).Where(p => Permissions.All.Contains(p)).ToHashSet();
            var current = (await _userManager.GetClaimsAsync(user)).Where(c => c.Type == Permissions.ClaimType).ToList();

            // İstenmeyenler ve artık tanımlı olmayan (eski) izin kayıtları temizlenir
            var toRemove = current.Where(c => !wanted.Contains(c.Value) || !Permissions.All.Contains(c.Value)).ToList();
            var toAdd = wanted.Where(p => current.All(c => c.Value != p))
                .Select(p => new Claim(Permissions.ClaimType, p)).ToList();

            var failed = false;
            if (toRemove.Count > 0)
                failed |= !(await _userManager.RemoveClaimsAsync(user, toRemove)).Succeeded;
            if (toAdd.Count > 0)
                failed |= !(await _userManager.AddClaimsAsync(user, toAdd)).Succeeded;

            // Değişiklik oturuma yansısın: kendi iznini değiştirdiyse hemen, başkası en geç 5 dk içinde.
            // Hiçbir şey değişmediyse kullanıcının oturumu boşuna yenilenmez.
            if (toRemove.Count > 0 || toAdd.Count > 0)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                if (user.Id.ToString() == _userManager.GetUserId(User))
                    await _signInManager.RefreshSignInAsync(user);
            }

            if (failed || shareFailed)
                TempData["Error"] = "İzinler ya da prim oranı kaydedilemedi. Lütfen tekrar deneyin.";
            else
                TempData["Success"] = $"{user.Name} {user.Surname} için izinler ve prim oranı güncellendi.";

            return RedirectToAction("Index", "AdminUser");
        }
    }
}
