using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Kullanıcı yönetimi (InstrumentTrade'deki AdminUserController karşılığı). Sadece Admin.
    // Farklar: yeni kullanıcı ekleme var; formlar ViewModel ile; aktif/pasif POST ile ve
    // pasif yapılınca açık oturum düşsün diye security stamp yenilenir; admin kendini pasif yapamaz.
    [Authorize(Roles = RoleNames.Admin)]
    public class AdminUserController : Controller
    {
        private readonly UserManager<AppUser> _userManager;

        public AdminUserController(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User);
            var users = _userManager.Users
                .OrderBy(u => u.Name).ThenBy(u => u.Surname)
                .ToList();

            var model = new List<AdminUserListItemViewModel>();
            foreach (var user in users)
            {
                model.Add(new AdminUserListItemViewModel
                {
                    Id = user.Id,
                    FullName = $"{user.Name} {user.Surname}",
                    UserName = user.UserName ?? "",
                    Email = user.Email,
                    IsActive = user.IsActive,
                    IsCurrentUser = user.Id.ToString() == currentUserId,
                    Roles = (await _userManager.GetRolesAsync(user)).ToList()
                });
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new AdminUserCreateViewModel { Role = RoleNames.Agent });
        }

        [HttpPost]
        public async Task<IActionResult> Create(AdminUserCreateViewModel model)
        {
            if (!RoleNames.All.Contains(model.Role))
                ModelState.AddModelError(nameof(model.Role), "Geçersiz rol.");

            if (!ModelState.IsValid)
                return View(model);

            var user = new AppUser
            {
                Name = model.Name.Trim(),
                Surname = model.Surname.Trim(),
                UserName = model.UserName.Trim(),
                Email = model.Email.Trim(),
                EmailConfirmed = true,
                IsActive = true,
                MustChangePassword = true   // admin şifreyi biliyor: kullanıcı ilk girişte kendi şifresini koyar
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
            {
                TempData["Error"] = $"{user.Name} {user.Surname} eklendi ama rol atanamadı. \"Roller\" butonundan rol atayın.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"{user.Name} {user.Surname} eklendi. Geçici şifreyi kullanıcıya iletin.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound();

            if (user.Id.ToString() == _userManager.GetUserId(User))
            {
                TempData["Error"] = "Kendi hesabınızı pasif yapamazsınız.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;

            // Pasif yaparken security stamp da yenilenir (açık oturum en geç doğrulama aralığında düşer).
            // UpdateSecurityStampAsync kullanıcıyı da kaydeder, ayrıca UpdateAsync gerekmez.
            var result = user.IsActive
                ? await _userManager.UpdateAsync(user)
                : await _userManager.UpdateSecurityStampAsync(user);

            if (!result.Succeeded)
            {
                TempData["Error"] = "İşlem kaydedilemedi: " + string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = user.IsActive
                ? $"{user.Name} {user.Surname} aktif yapıldı."
                : $"{user.Name} {user.Surname} pasif yapıldı.";

            return RedirectToAction(nameof(Index));
        }
    }
}
