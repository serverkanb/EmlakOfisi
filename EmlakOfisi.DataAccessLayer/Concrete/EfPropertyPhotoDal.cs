using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    public class EfPropertyPhotoDal : IPropertyPhotoDal
    {
        private readonly AppDbContext _context;

        public EfPropertyPhotoDal(AppDbContext context)
        {
            _context = context;
        }

        public List<PropertyPhoto> GetListByProperty(int propertyId)
        {
            return _context.PropertyPhotos
                .AsNoTracking()
                .Where(p => p.PropertyId == propertyId)
                .OrderByDescending(p => p.IsCover).ThenBy(p => p.SortOrder).ThenBy(p => p.PropertyPhotoId)
                .ToList();
        }

        public int CountByProperty(int propertyId)
        {
            return _context.PropertyPhotos.Count(p => p.PropertyId == propertyId);
        }

        public PropertyPhoto? GetById(int propertyPhotoId)
        {
            return _context.PropertyPhotos.AsNoTracking().FirstOrDefault(p => p.PropertyPhotoId == propertyPhotoId);
        }

        public PropertyPhoto? GetByIdForUpdate(int propertyPhotoId)
        {
            return _context.PropertyPhotos.FirstOrDefault(p => p.PropertyPhotoId == propertyPhotoId);
        }

        public void Create(PropertyPhoto photo)
        {
            // Sıra: portföyün son fotoğrafından sonra
            photo.SortOrder = (_context.PropertyPhotos
                .Where(p => p.PropertyId == photo.PropertyId)
                .Max(p => (int?)p.SortOrder) ?? 0) + 1;

            _context.PropertyPhotos.Add(photo);
            _context.SaveChanges();
        }

        public void Delete(PropertyPhoto photo)
        {
            _context.PropertyPhotos.Remove(photo);
            _context.SaveChanges();
        }

        public void SetCover(int propertyId, int propertyPhotoId)
        {
            foreach (var p in _context.PropertyPhotos.Where(p => p.PropertyId == propertyId))
                p.IsCover = p.PropertyPhotoId == propertyPhotoId;
            _context.SaveChanges();
        }
    }
}
