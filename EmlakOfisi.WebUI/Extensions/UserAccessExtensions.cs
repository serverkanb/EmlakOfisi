using System.Security.Claims;
using EmlakOfisi.EntityLayer.Constants;

namespace EmlakOfisi.WebUI.Extensions
{
    // Controller'larda "kim giriş yaptı, her şeyi görebilir mi" bilgisini tek yerden okumak için.
    // Kullanım: _contactService.TGetVisibleById(id, User.GetUserId(), User.CanSeeAll())
    public static class UserAccessExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(value, out var id))
                throw new InvalidOperationException("Giriş yapmış kullanıcı bulunamadı.");
            return id;
        }

        // Admin ve Manager (ofis yöneticisi) tüm kayıtları görür; Agent ve rolsüz kullanıcı görmez.
        // Rolsüz kullanıcı iş ekranlarına zaten giremez: [Authorize(Roles = RoleNames.Staff)].
        public static bool CanSeeAll(this ClaimsPrincipal user)
        {
            return user.IsInRole(RoleNames.Admin) || user.IsInRole(RoleNames.Manager);
        }

        // Admin'in verdiği ek izin (Permissions). Sadece tanımlı izinler sayılır.
        public static bool HasPermission(this ClaimsPrincipal user, string permission)
        {
            return Permissions.All.Contains(permission) && user.HasClaim(Permissions.ClaimType, permission);
        }
    }
}
