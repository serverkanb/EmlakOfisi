namespace EmlakOfisi.WebUI.Helpers
{
    // Fotoğraf dosyalarını wwwroot DIŞINDA saklar (varsayılan: App_Data/uploads). Dosyalar adresle doğrudan
    // açılamaz; sadece PropertyPhotoController yetki kontrolünden sonra verir.
    // Dosya adı her zaman sunucuda üretilir: "{portföyId}/{guid}{uzantı}". Kullanıcının dosya adı yol olarak kullanılmaz.
    public class PhotoFileStore
    {
        private readonly string _root;

        public PhotoFileStore(string rootPath)
        {
            // Ayar "D:\uploads\" gibi sonunda ayraçla yazılsa da aynı klasör kabul edilsin
            _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        }

        // Yazılan dosyanın göreli adını döner (veritabanına bu kaydedilir)
        public async Task<string> SaveAsync(int propertyId, Stream content, string extension)
        {
            var fileName = $"{propertyId}/{Guid.NewGuid():N}{extension}";
            var fullPath = Resolve(fileName)
                ?? throw new InvalidOperationException("Fotoğraf yolu yükleme klasörünün dışında: " + fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            try
            {
                await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write);
                await content.CopyToAsync(file);
            }
            catch
            {
                // Yarım kalan dosya diskte durmasın
                if (File.Exists(fullPath)) File.Delete(fullPath);
                throw;
            }
            return fileName;
        }

        // Dosya varsa tam yolu; yoksa ya da kök klasörün dışını gösteriyorsa null
        public string? GetPath(string fileName)
        {
            var fullPath = Resolve(fileName);
            return fullPath != null && File.Exists(fullPath) ? fullPath : null;
        }

        public void Delete(string fileName)
        {
            var fullPath = Resolve(fileName);
            if (fullPath != null && File.Exists(fullPath))
                File.Delete(fullPath);
        }

        // "..\" gibi yollarla kök klasörün dışına çıkılamaz
        private string? Resolve(string fileName)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_root, fileName));
            return fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }
    }
}
