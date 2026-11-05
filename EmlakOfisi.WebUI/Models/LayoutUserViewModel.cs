namespace EmlakOfisi.WebUI.Models
{
    // Üst menüdeki kullanıcı kutusu (ad, rol, baş harfler)
    public class LayoutUserViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
    }
}
