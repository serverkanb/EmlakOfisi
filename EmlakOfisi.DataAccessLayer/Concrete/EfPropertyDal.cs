using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    // Müşterideki EfContactDal ile aynı yapı (generic'ten türemez).
    public class EfPropertyDal : IPropertyDal
    {
        private readonly AppDbContext _context;

        public EfPropertyDal(AppDbContext context)
        {
            _context = context;
        }

        // Görünürlük kuralı VisibilityRules'ta (tek yer)
        private IQueryable<Property> Visible(int userId, bool canSeeAll) =>
            _context.Properties.VisibleTo(userId, canSeeAll);

        private IQueryable<Property> Filtered(int userId, bool canSeeAll, PropertyFilter f)
        {
            var query = Visible(userId, canSeeAll);

            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                // Büyük/küçük harf ve Türkçe karakter eşleşmesi kolon collation'ından gelir (Turkish_CI_AS)
                var term = f.Search.Trim();
                query = query.Where(p =>
                    p.Title.Contains(term) ||
                    p.City.Contains(term) ||
                    p.District.Contains(term) ||
                    (p.Neighborhood != null && p.Neighborhood.Contains(term)));
            }

            if (f.ListingType != null) query = query.Where(p => p.ListingType == f.ListingType);
            if (f.PropertyType != null) query = query.Where(p => p.PropertyType == f.PropertyType);
            if (f.Status != null) query = query.Where(p => p.Status == f.Status);
            if (f.MinPrice != null) query = query.Where(p => p.Price >= f.MinPrice);
            if (f.MaxPrice != null) query = query.Where(p => p.Price <= f.MaxPrice);
            if (f.MinArea != null) query = query.Where(p => (p.NetArea ?? p.GrossArea) >= f.MinArea);
            if (f.OnlyMine) query = query.Where(p => p.CreatedByUserId == userId || p.AgentUserId == userId);

            return query;
        }

        public List<Property> GetVisibleList(int userId, bool canSeeAll, PropertyFilter filter, int skip, int take)
        {
            return Filtered(userId, canSeeAll, filter)
                .AsNoTracking()
                .Include(p => p.AgentUser)
                .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.PropertyId)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public int CountVisible(int userId, bool canSeeAll, PropertyFilter filter)
        {
            return Filtered(userId, canSeeAll, filter).Count();
        }

        public List<(PropertyStatus Status, ListingType ListingType, int Count)> CountVisibleByStatus(int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll)
                .GroupBy(p => new { p.Status, p.ListingType })
                .Select(g => new { g.Key.Status, g.Key.ListingType, Count = g.Count() })
                .ToList()
                .Select(x => (x.Status, x.ListingType, x.Count))
                .ToList();
        }

        public Property? GetVisibleById(int id, int userId, bool canSeeAll)
        {
            // Mülk sahibi (OwnerContact) burada YÜKLENMEZ: o kişinin görünürlüğü ayrıca kontrol edilir (servis).
            return Visible(userId, canSeeAll)
                .AsNoTracking()
                .Include(p => p.AgentUser)
                .FirstOrDefault(p => p.PropertyId == id);
        }

        public Property? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll).FirstOrDefault(p => p.PropertyId == id);
        }

        public bool IsActiveStaff(int userId)
        {
            return _context.Users.Any(u => u.Id == userId && u.IsActive) &&
                   _context.UserRoles.Any(ur => ur.UserId == userId);
        }

        public List<(int Id, string FullName)> GetActiveStaff()
        {
            var staffIds = _context.UserRoles.Select(ur => ur.UserId);
            return _context.Users
                .Where(u => u.IsActive && staffIds.Contains(u.Id))
                .OrderBy(u => u.Name).ThenBy(u => u.Surname)
                .Select(u => new { u.Id, u.Name, u.Surname })
                .AsEnumerable()
                .Select(u => (u.Id, $"{u.Name} {u.Surname}"))
                .ToList();
        }

        public void Create(Property property)
        {
            _context.Properties.Add(property);
            _context.SaveChanges();
        }

        public void Update(Property property)
        {
            // Formdan gelen, takip edilmeyen bir nesne ile tüm kolonların ezilmesini engeller
            if (_context.Entry(property).State == EntityState.Detached)
                throw new InvalidOperationException("Güncellenecek portföy önce GetVisibleByIdForUpdate ile okunmalıdır.");

            _context.SaveChanges();
        }
    }
}
