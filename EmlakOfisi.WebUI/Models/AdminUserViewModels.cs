using System.ComponentModel.DataAnnotations;

namespace EmlakOfisi.WebUI.Models
{
    // Kullanıcı listesindeki bir satır
    public class AdminUserListItemViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public bool IsCurrentUser { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    // Yeni kullanıcı formu (entity yerine ViewModel: formdan IsActive vb. gönderilemez)
    public class AdminUserCreateViewModel
    {
        [Required(ErrorMessage = "Ad zorunludur.")]
        [StringLength(50)]
        [Display(Name = "Ad")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad zorunludur.")]
        [StringLength(50)]
        [Display(Name = "Soyad")]
        public string Surname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [StringLength(50)]
        [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Kullanıcı adında sadece harf (Türkçe karakter hariç), rakam, nokta, tire ve alt çizgi kullanılabilir.")]
        [Display(Name = "Kullanıcı adı")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        [StringLength(150)]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [DataType(DataType.Password)]
        [Display(Name = "Geçici şifre")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rol seçin.")]
        [Display(Name = "Rol")]
        public string Role { get; set; } = string.Empty;
    }

    // Rol atama ekranındaki bir satır (InstrumentTrade'deki RoleAssignViewModel ile aynı)
    public class RoleAssignViewModel
    {
        public string RoleName { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public bool RoleExist { get; set; }
    }

    // Rol atama sayfası: kullanıcı bilgisi + roller
    public class AssignRolePageViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public List<RoleAssignViewModel> Roles { get; set; } = new();
    }

    // Kullanıcının ek izinleri (görev 17). Sadece Admin görür.
    public class UserPermissionItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Granted { get; set; }
    }

    public class UserPermissionPageViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool CanSeeAll { get; set; }   // yönetici: izin olmadan da her şeyi düzenleyebilir
        public List<UserPermissionItemViewModel> Permissions { get; set; } = new();
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mevcut şifre")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifre zorunludur.")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifre")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifreyi tekrar girin.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Şifreler uyuşmuyor.")]
        [Display(Name = "Yeni şifre (tekrar)")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
