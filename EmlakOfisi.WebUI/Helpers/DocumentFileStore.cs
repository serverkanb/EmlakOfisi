namespace EmlakOfisi.WebUI.Helpers
{
    // Kontrat dosyaları (görev 22): fotoğraflarla aynı güvenli saklama, ayrı kök klasör (varsayılan: App_Data/documents).
    // Ayrı tip olması DI'da fotoğraf deposuyla karışmasın diye; dosya adı "{işlemId}/{guid}{uzantı}".
    public sealed class DocumentFileStore : PhotoFileStore
    {
        public DocumentFileStore(string rootPath) : base(rootPath) { }
    }
}
