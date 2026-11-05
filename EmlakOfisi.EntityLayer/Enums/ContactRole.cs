namespace EmlakOfisi.EntityLayer.Enums
{
    // Müşteri listesinde rol filtresi için (Contact.IsBuyer, IsSeller... alanlarına karşılık gelir).
    // Veritabanında saklanmaz; kullanıcı rolleriyle (RoleNames) karıştırılmamalı.
    public enum ContactRole
    {
        Buyer = 1,      // Alıcı
        Seller = 2,     // Satıcı
        Tenant = 3,     // Kiracı
        Landlord = 4    // Mülk sahibi
    }
}
