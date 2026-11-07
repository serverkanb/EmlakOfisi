using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.BusinessLayer.Concrete
{
    public class PropertyPhotoManager : IPropertyPhotoService
    {
        private readonly IPropertyPhotoDal _photoDal;
        private readonly IPropertyService _propertyService;

        // Portföy görünürlüğü ve düzenleme kuralı tek yerde kalsın diye IPropertyService kullanılır
        public PropertyPhotoManager(IPropertyPhotoDal photoDal, IPropertyService propertyService)
        {
            _photoDal = photoDal;
            _propertyService = propertyService;
        }

        public List<PropertyPhoto>? TGetForProperty(int propertyId, int userId, bool canSeeAll)
        {
            if (_propertyService.TGetVisibleById(propertyId, userId, canSeeAll) == null)
                return null;

            return _photoDal.GetListByProperty(propertyId);
        }

        public PropertyPhoto? TGetVisible(int propertyPhotoId, int userId, bool canSeeAll)
        {
            var photo = _photoDal.GetById(propertyPhotoId);
            if (photo == null || _propertyService.TGetVisibleById(photo.PropertyId, userId, canSeeAll) == null)
                return null;
            return photo;
        }

        public OperationResult TCheckUpload(int propertyId, int userId, bool canSeeAll, out int remaining)
        {
            remaining = 0;
            var check = CheckEdit(propertyId, userId, canSeeAll);
            if (check != OperationResult.Success)
                return check;

            remaining = IPropertyPhotoService.MaxPerProperty - _photoDal.CountByProperty(propertyId);
            return remaining > 0 ? OperationResult.Success : OperationResult.Invalid;
        }

        public OperationResult TAdd(int propertyId, string fileName, string contentType, string? originalName, int userId, bool canSeeAll)
        {
            var check = TCheckUpload(propertyId, userId, canSeeAll, out _);
            if (check != OperationResult.Success)
                return check;

            var isFirst = _photoDal.CountByProperty(propertyId) == 0;
            _photoDal.Create(new PropertyPhoto
            {
                PropertyId = propertyId,
                FileName = fileName,
                ContentType = contentType,
                OriginalName = CleanOriginalName(originalName),
                IsCover = isFirst,
                UploadedByUserId = userId,
                UploadedAt = DateTime.Now
            });
            return OperationResult.Success;
        }

        public OperationResult TDelete(int propertyPhotoId, int userId, bool canSeeAll, out int propertyId, out string fileName)
        {
            propertyId = 0;
            fileName = string.Empty;

            var photo = _photoDal.GetByIdForUpdate(propertyPhotoId);
            if (photo == null)
                return OperationResult.NotFound;

            var check = CheckEdit(photo.PropertyId, userId, canSeeAll);
            if (check != OperationResult.Success)
                return check;

            propertyId = photo.PropertyId;
            fileName = photo.FileName;
            var wasCover = photo.IsCover;
            _photoDal.Delete(photo);

            // Kapak silindiyse sıradaki fotoğraf kapak olur
            if (wasCover)
            {
                var next = _photoDal.GetListByProperty(propertyId).FirstOrDefault();
                if (next != null)
                    _photoDal.SetCover(propertyId, next.PropertyPhotoId);
            }
            return OperationResult.Success;
        }

        public OperationResult TSetCover(int propertyPhotoId, int userId, bool canSeeAll, out int propertyId)
        {
            propertyId = 0;
            var photo = _photoDal.GetById(propertyPhotoId);
            if (photo == null)
                return OperationResult.NotFound;

            var check = CheckEdit(photo.PropertyId, userId, canSeeAll);
            if (check != OperationResult.Success)
                return check;

            propertyId = photo.PropertyId;
            _photoDal.SetCover(photo.PropertyId, photo.PropertyPhotoId);
            return OperationResult.Success;
        }

        // Kullanıcının dosya adı sadece bilgi amaçlı saklanır; diskte yol olarak hiç kullanılmaz
        private static string? CleanOriginalName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            var clean = Path.GetFileName(name).Trim();
            return clean.Length <= 260 ? clean : clean[..260];
        }

        // Görmeyen 404 (varlığı belli olmaz), görüp düzenleyemeyen 403
        private OperationResult CheckEdit(int propertyId, int userId, bool canSeeAll)
        {
            var property = _propertyService.TGetVisibleById(propertyId, userId, canSeeAll);
            if (property == null)
                return OperationResult.NotFound;
            return _propertyService.TCanEdit(property, userId, canSeeAll) ? OperationResult.Success : OperationResult.Forbidden;
        }
    }
}
