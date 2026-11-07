using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Portföy fotoğrafları (görev 13). Galeri portföy detayında; burada gösterim, yükleme, silme ve kapak seçme var.
    // Yetki serviste: portföy görünmüyorsa 404, düzenleme yetkisi yoksa 403 (dosya kontrollerinden ÖNCE).
    [Authorize(Roles = RoleNames.Staff)]
    public class PropertyPhotoController : Controller
    {
        public const int MaxFilesPerUpload = 10;

        private readonly IPropertyPhotoService _photoService;
        private readonly PhotoFileStore _store;
        private readonly ILogger<PropertyPhotoController> _logger;

        public PropertyPhotoController(IPropertyPhotoService photoService, PhotoFileStore store, ILogger<PropertyPhotoController> logger)
        {
            _photoService = photoService;
            _store = store;
            _logger = logger;
        }

        // Fotoğraf dosyası: sadece portföyü görebilen kişiye
        [HttpGet]
        public IActionResult Show(int id)
        {
            var photo = _photoService.TGetVisible(id, User.GetUserId(), User.CanSeeAll());
            if (photo == null)
                return NotFound();

            var path = _store.GetPath(photo.FileName);
            if (path == null)
                return NotFound();

            // Tarayıcı en fazla 1 saat önbellekte tutar (ortak bilgisayarda uzun kalmasın); proxy önbellekleri tutmaz
            Response.Headers.CacheControl = "private, max-age=3600";
            Response.Headers.XContentTypeOptions = "nosniff";
            // Dosya doğrudan açılsa bile içinde betik çalışamasın (ek savunma)
            Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            return PhysicalFile(path, photo.ContentType);
        }

        [HttpPost]
        public async Task<IActionResult> Upload(int propertyId, List<IFormFile>? files)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            // Önce yetki (dosyalara bakmadan): göremeyen 404, düzenleyemeyen 403
            var check = _photoService.TCheckUpload(propertyId, userId, canSeeAll, out var remaining);
            if (check == OperationResult.NotFound)
                return NotFound();
            if (check == OperationResult.Forbidden)
                return Forbid();
            if (check == OperationResult.Invalid)
            {
                TempData["Error"] = $"Bir portföye en fazla {IPropertyPhotoService.MaxPerProperty} fotoğraf eklenebilir.";
                return BackToProperty(propertyId);
            }

            files ??= new();
            if (files.Count == 0)
            {
                TempData["Error"] = "Lütfen en az bir fotoğraf seçin.";
                return BackToProperty(propertyId);
            }

            var errors = new List<string>();
            var added = 0;
            foreach (var file in files.Take(Math.Min(MaxFilesPerUpload, remaining)))
            {
                var error = await SaveOneAsync(propertyId, file, userId, canSeeAll);
                if (error == null) added++;
                else errors.Add(error);
            }

            var skipped = files.Count - Math.Min(MaxFilesPerUpload, remaining);
            if (skipped > 0)
                errors.Add($"{skipped} fotoğraf eklenmedi (tek seferde en fazla {MaxFilesPerUpload}, portföy başına en fazla {IPropertyPhotoService.MaxPerProperty}).");

            if (added > 0)
                TempData["Success"] = $"{added} fotoğraf eklendi.";
            if (errors.Count > 0)
                TempData["Error"] = string.Join(" ", errors.Distinct());

            return BackToProperty(propertyId);
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var result = _photoService.TDelete(id, User.GetUserId(), User.CanSeeAll(), out var propertyId, out var fileName);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            try
            {
                _store.Delete(fileName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Kayıt silindi; dosya kalırsa sadece disk yer kaplar, kimseye gösterilmez
                _logger.LogWarning(ex, "Fotoğraf dosyası silinemedi: {FileName}", fileName);
            }

            TempData["Success"] = "Fotoğraf silindi.";
            return BackToProperty(propertyId);
        }

        [HttpPost]
        public IActionResult SetCover(int id)
        {
            var result = _photoService.TSetCover(id, User.GetUserId(), User.CanSeeAll(), out var propertyId);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            TempData["Success"] = "Kapak fotoğrafı değiştirildi.";
            return BackToProperty(propertyId);
        }

        // Tek dosyayı doğrular ve kaydeder; hata varsa kullanıcıya gösterilecek mesajı döner
        private async Task<string?> SaveOneAsync(int propertyId, IFormFile file, int userId, bool canSeeAll)
        {
            if (file.Length == 0)
                return "Boş dosya eklenmedi.";
            if (file.Length > ImageFileValidator.MaxBytes)
                return "5 MB'tan büyük fotoğraf eklenmedi.";

            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var read = await stream.ReadAsync(header);
            var type = ImageFileValidator.Detect(header.AsSpan(0, read));
            if (type == null)
                return "Sadece JPEG, PNG ya da WebP fotoğraf eklenebilir.";

            stream.Position = 0;
            string fileName;
            try
            {
                fileName = await _store.SaveAsync(propertyId, stream, type.Value.Extension);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Sunucuda klasöre yazma izni yoksa ya da disk doluysa (paylaşımlı hosting kotası)
                _logger.LogError(ex, "Fotoğraf diske yazılamadı (portföy {PropertyId}).", propertyId);
                return "Fotoğraf sunucuya kaydedilemedi. Lütfen yöneticinize bildirin.";
            }

            OperationResult result;
            try
            {
                result = _photoService.TAdd(propertyId, fileName, type.Value.ContentType, file.FileName, userId, canSeeAll);
            }
            catch
            {
                _store.Delete(fileName); // kayıt eklenemediyse dosya diskte sahipsiz kalmasın
                throw;
            }

            if (result != OperationResult.Success)
            {
                _store.Delete(fileName);
                return "Fotoğraf eklenemedi.";
            }
            return null;
        }

        private IActionResult BackToProperty(int propertyId) =>
            RedirectToAction("Detail", "Property", new { id = propertyId }, "fotograflar");
    }
}
