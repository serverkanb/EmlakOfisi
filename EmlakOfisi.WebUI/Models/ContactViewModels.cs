using System.ComponentModel.DataAnnotations;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.WebUI.Models
{
    // Müşteri listesindeki bir satır/kart
    public class ContactListItemViewModel
    {
        public int ContactId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string TelLink { get; set; } = string.Empty;       // tel:+905321112233
        public string WhatsAppLink { get; set; } = string.Empty;  // https://wa.me/905321112233
        public bool HasPhoneLinks => TelLink.Length > 0;
        public bool IsBuyer { get; set; }
        public bool IsSeller { get; set; }
        public bool IsTenant { get; set; }
        public bool IsLandlord { get; set; }
        public Visibility Visibility { get; set; }
        public bool IsMine { get; set; }
    }

    public class ContactListViewModel
    {
        public List<ContactListItemViewModel> Items { get; set; } = new();
        public string? Search { get; set; }
        public ContactRole? Role { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    // Müşteri ekleme / düzenleme formu.
    // Sadece kullanıcının değiştirebileceği alanlar var: sahip (CreatedByUserId), tarih ve silinme durumu
    // formda YOK, bu yüzden formdan sahte değerle gönderilemez (over-posting).
    public class ContactFormViewModel
    {
        [Required(ErrorMessage = "Ad zorunludur.")]
        [StringLength(50, ErrorMessage = "Ad en fazla 50 karakter olabilir.")]
        [Display(Name = "Ad")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Soyad zorunludur.")]
        [StringLength(50, ErrorMessage = "Soyad en fazla 50 karakter olabilir.")]
        [Display(Name = "Soyad")]
        public string Surname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefon zorunludur.")]
        [StringLength(20, ErrorMessage = "Telefon en fazla 20 karakter olabilir.")]
        [Display(Name = "Telefon")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "2. telefon en fazla 20 karakter olabilir.")]
        [Display(Name = "2. telefon")]
        public string? Phone2 { get; set; }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        [StringLength(150, ErrorMessage = "E-posta en fazla 150 karakter olabilir.")]
        [Display(Name = "E-posta")]
        public string? Email { get; set; }

        [StringLength(2000, ErrorMessage = "Not en fazla 2000 karakter olabilir.")]
        [Display(Name = "Notlar")]
        public string? Notes { get; set; }

        [Display(Name = "Alıcı")] public bool IsBuyer { get; set; }
        [Display(Name = "Satıcı")] public bool IsSeller { get; set; }
        [Display(Name = "Kiracı")] public bool IsTenant { get; set; }
        [Display(Name = "Mülk sahibi")] public bool IsLandlord { get; set; }

        // Varsayılan "Sadece Ben" (CLAUDE.md kural 2)
        [Display(Name = "Kim görebilir?")]
        public Visibility Visibility { get; set; } = Visibility.OnlyMe;
    }

    // Müşteri detayı (salt okunur; düzenleme görev 9)
    public class ContactDetailViewModel
    {
        public ContactListItemViewModel Summary { get; set; } = new();
        public string? Phone2 { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }
}
