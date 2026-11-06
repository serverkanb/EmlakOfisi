namespace EmlakOfisi.BusinessLayer.Abstract
{
    // Servis işlemlerinin ortak sonucu (müşteri, görüşme, portföy).
    // Controller buna göre 404, 403, form hatası ya da başarı mesajı döner.
    public enum OperationResult
    {
        Success,
        NotFound,    // kayıt yok veya kullanıcı göremiyor (404; varlığı belli edilmez)
        Forbidden,   // görüyor ama bu işlem için yetkisi yok (403)
        Invalid      // bağlı bir değer geçersiz (ör. göremediği bir kişiyi mülk sahibi seçmek) -> form hatası
    }
}
