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
