using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    public class EfCustomerRequestDal : ICustomerRequestDal
    {
        private readonly AppDbContext _context;

        public EfCustomerRequestDal(AppDbContext context)
        {
            _context = context;
        }

        // Talep, bağlı müşteri kullanıcıya görünüyorsa görünür (kural VisibilityRules'ta, tek yerde)
        private IQueryable<CustomerRequest> Visible(int userId, bool canSeeAll)
        {
            var contacts = _context.Contacts.VisibleTo(userId, canSeeAll);
            return _context.CustomerRequests.Where(r => contacts.Any(c => c.ContactId == r.ContactId));
        }

        private IQueryable<CustomerRequest> Filtered(int userId, bool canSeeAll, CustomerRequestFilter f)
        {
            var query = Visible(userId, canSeeAll);

            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                // Türkçe büyük/küçük harf eşleşmesi kolon collation'ından gelir (Turkish_CI_AS)
                var term = f.Search.Trim();
                query = query.Where(r => (r.City != null && r.City.Contains(term)) ||
                                         (r.District != null && r.District.Contains(term)));
            }
            if (f.ListingType != null) query = query.Where(r => r.ListingType == f.ListingType);
            if (f.PropertyType != null) query = query.Where(r => r.PropertyType == f.PropertyType);
            if (f.Status != null) query = query.Where(r => r.Status == f.Status);
            if (f.ContactId != null) query = query.Where(r => r.ContactId == f.ContactId);
            return query;
        }

        public List<CustomerRequestRow> GetVisibleList(int userId, bool canSeeAll, CustomerRequestFilter filter, int skip, int take)
        {
            return Filtered(userId, canSeeAll, filter)
                .AsNoTracking()
                .OrderBy(r => r.Status == RequestStatus.Open ? 0 : 1)   // açıklar üstte
                .ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.CustomerRequestId)
                .Skip(skip)
                .Take(take)
                .Select(r => new CustomerRequestRow
                {
                    Request = r,
                    ContactName = r.Contact!.Name + " " + r.Contact.Surname,
                    ContactPhoneNormalized = r.Contact.PhoneNormalized,
                    CreatedByName = r.CreatedByUser!.Name + " " + r.CreatedByUser.Surname
                })
                .ToList();
        }

        public int CountVisible(int userId, bool canSeeAll, CustomerRequestFilter filter)
        {
            return Filtered(userId, canSeeAll, filter).Count();
        }

        public CustomerRequest? GetVisibleById(int id, int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll).AsNoTracking().FirstOrDefault(r => r.CustomerRequestId == id);
        }

        public CustomerRequest? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll).FirstOrDefault(r => r.CustomerRequestId == id);
        }

        public void Create(CustomerRequest request)
        {
            _context.CustomerRequests.Add(request);
            _context.SaveChanges();
        }

        public void Update(CustomerRequest request)
        {
            if (_context.Entry(request).State == EntityState.Detached)
                throw new InvalidOperationException("Güncellenecek talep önce GetVisibleByIdForUpdate ile okunmalıdır.");

            _context.SaveChanges();
        }
    }
}
