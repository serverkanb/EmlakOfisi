namespace EmlakOfisi.EntityLayer.Constants
{
    // Rol adları tek yerde. InstrumentTrade'de "Admin" / "Customer" metin olarak yazılıyordu;
    // burada yazım hatası olmasın diye sabit kullanılıyor: [Authorize(Roles = RoleNames.Admin)]
    public static class RoleNames
    {
        public const string Admin = "Admin";       // Sistem yöneticisi (kullanıcı/rol yönetimi)
        public const string Manager = "Manager";   // Ofis yöneticisi (tüm kayıtları görür)
        public const string Agent = "Agent";       // Danışman

        public static readonly string[] All = { Admin, Manager, Agent };

        // Ekranda gösterilecek Türkçe ad
        public static string Label(string role) => role switch
        {
            Admin => "Sistem yöneticisi",
            Manager => "Ofis yöneticisi",
            Agent => "Danışman",
            _ => role
        };
    }
}
