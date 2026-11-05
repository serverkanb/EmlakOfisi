using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Rol atama (InstrumentTrade'deki RoleAssignController ile aynı akış). Sadece Admin.
    // Farklar: kullanıcı id'si TempData yerine adresten gelir (/RoleAssign/AssignRole/5);
    // sadece tanımlı roller (RoleNames.All) atanabilir; admin kendi Admin rolünü kaldıramaz;
    // rol değişince kullanıcının oturumu yenilensin diye security stamp güncellenir.
    // Rol ekleme/silme ekranı (InstrumentTrade'deki RoleController) yok: roller sabit.
    [Authorize(Roles = RoleNames.Admin)]
    public class RoleAssignController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public RoleAssignController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public async Task<IActionResult> AssignRole(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new AssignRolePageViewModel
            {
                UserId = user.Id,
                FullName = $"{user.Name} {user.Surname}",
                Roles = RoleNames.All.Select(role => new RoleAssignViewModel
                {
                    RoleName = role,
                    RoleLabel = RoleNames.Label(role),
                    RoleExist = userRoles.Contains(role)
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> AssignRole(int id, List<RoleAssignViewModel> roles)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            // Rolsüz kullanıcı olmasın: en az bir geçerli rol seçilmeli
            if (!roles.Any(r => r.RoleExist && RoleNames.All.Contains(r.RoleName)))
            {
                TempData["Error"] = "En az bir rol seçmelisiniz. Değişiklik yapılmadı.";
                return RedirectToAction(nameof(AssignRole), new { id });
            }

            var isSelf = user.Id.ToString() == _userManager.GetUserId(User);
            var currentRoles = await _userManager.GetRolesAsync(user);
            var blocked = false;

            foreach (var item in roles.Where(r => RoleNames.All.Contains(r.RoleName)))
            {
                var has = currentRoles.Contains(item.RoleName);

                if (item.RoleExist && !has)
                {
                    await _userManager.AddToRoleAsync(user, item.RoleName);
                }
                else if (!item.RoleExist && has)
                {
                    if (isSelf && item.RoleName == RoleNames.Admin)
                    {
                        blocked = true;
                        continue;
                    }

                    await _userManager.RemoveFromRoleAsync(user, item.RoleName);
                }
            }

            // Rol değişikliği oturuma yansısın: başkasının oturumu yenilenmek zorunda kalır,
            // kendi rolünü değiştirdiyse oturumu hemen yenilenir.
            await _userManager.UpdateSecurityStampAsync(user);
            if (isSelf)
                await _signInManager.RefreshSignInAsync(user);

            if (blocked)
                TempData["Error"] = "Kendi Admin rolünüzü kaldıramazsınız. Diğer değişiklikler kaydedildi.";
            else
                TempData["Success"] = $"{user.Name} {user.Surname} için roller güncellendi.";

            return RedirectToAction("Index", "AdminUser");
        }
    }
}
