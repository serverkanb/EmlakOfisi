namespace EmlakOfisi.WebUI.Helpers
{
    // Yüklenen dosyaları wwwroot DIŞINDA saklar (fotoğraflar: App_Data/uploads; kontratlar DocumentFileStore ile
    // App_Data/documents). Dosyalar adresle doğrudan açılamaz; sadece controller yetki kontrolünden sonra verir.
    // Dosya adı her zaman sunucuda üretilir: "{kayıtId}/{guid}{uzantı}". Kullanıcının dosya adı yol olarak kullanılmaz.
    public class PhotoFileStore
    {
        private readonly string _root;

        public PhotoFileStore(string rootPath)
        {
            // Ayar "D:\uploads\" gibi sonunda ayraçla yazılsa da aynı klasör kabul edilsin
            _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        }

        // Yazılan dosyanın göreli adını döner (veritabanına bu kaydedilir)
        public async Task<string> SaveAsync(int ownerId, Stream content, string extension)
        {
            var fileName = $"{ownerId}/{Guid.NewGuid():N}{extension}";
            var fullPath = Resolve(fileName)
                ?? throw new InvalidOperationException("Dosya yolu kök klasörün dışında: " + fileName);
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

        // Kök klasör web kökünün kendisi ya da altında mı (açılış kontrolü, Program.cs)
        public static bool IsInside(string root, string? webRoot)
        {
            if (string.IsNullOrWhiteSpace(webRoot)) return false;
            var r = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var w = Path.TrimEndingDirectorySeparator(Path.GetFullPath(webRoot));
            return r.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                   r.StartsWith(w + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        // "..\" gibi yollarla kök klasörün dışına çıkılamaz
        private string? Resolve(string fileName)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_root, fileName));
            return fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }
    }
}
