namespace EmlakOfisi.EntityLayer.Constants
{
    // Admin'in kişi bazında verdiği ek izinler (görev 17). Identity'nin kullanıcı claim tablosunda
    // ClaimType = "permission", ClaimValue = izin adı olarak saklanır; giriş yapınca oturuma (cookie) girer.
    // İzinler GÖRÜNÜRLÜĞÜ değiştirmez: sadece görebildiği kayıtlar üzerinde ek işlem yetkisi verir.
    public static class Permissions
    {
        public const string ClaimType = "permission";

        // Görebildiği her müşteriye (başka danışmanın herkese açık müşterisi dahil) talep ekleyip düzenleyebilir
        public const string RequestEditAny = "request.edit-any";

        public static readonly string[] All = { RequestEditAny };

        // Ekranda gösterilecek Türkçe ad
        public static string Label(string permission) => permission switch
        {
            RequestEditAny => "Başkasının müşterisine talep ekleyip düzenleyebilir",
            _ => permission
        };

        public static string Description(string permission) => permission switch
        {
            RequestEditAny => "Görebildiği tüm müşterilerin taleplerini (başka danışmanların herkese açık müşterileri dahil) ekler, düzenler, kapatır. Gizli müşterileri yine göremez.",
            _ => string.Empty
        };
    }
}
