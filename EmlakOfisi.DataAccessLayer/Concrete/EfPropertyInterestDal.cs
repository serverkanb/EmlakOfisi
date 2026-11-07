using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    public class EfPropertyInterestDal : IPropertyInterestDal
    {
        public const int MaxItems = 200;

        private readonly AppDbContext _context;

        public EfPropertyInterestDal(AppDbContext context)
        {
            _context = context;
        }

        public List<PropertyInterest> GetListByProperty(int propertyId, int userId, bool canSeeAll)
        {
            // Görünmeyen müşterinin satırı hiç gelmez (sayısı da belli olmaz)
            var visibleContacts = _context.Contacts.VisibleTo(userId, canSeeAll);

            return _context.PropertyInterests
                .AsNoTracking()
                .Include(i => i.Contact)
                .Include(i => i.CreatedByUser)
                .Where(i => i.PropertyId == propertyId && visibleContacts.Any(c => c.ContactId == i.ContactId))
                .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.PropertyInterestId)
                .Take(MaxItems)
                .ToList();
        }

        public List<PropertyInterest> GetListByContact(int contactId, int userId, bool canSeeAll)
        {
            // Görünmeyen portföyün satırı hiç gelmez
            var visibleProperties = _context.Properties.VisibleTo(userId, canSeeAll);

            return _context.PropertyInterests
                .AsNoTracking()
                .Include(i => i.Property)
                .Include(i => i.CreatedByUser)
                .Where(i => i.ContactId == contactId && visibleProperties.Any(p => p.PropertyId == i.PropertyId))
                .OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.PropertyInterestId)
                .Take(MaxItems)
                .ToList();
        }

        public bool Exists(int propertyId, int contactId)
        {
            return _context.PropertyInterests.Any(i => i.PropertyId == propertyId && i.ContactId == contactId);
        }

        public PropertyInterest? GetByIdForUpdate(int propertyInterestId)
        {
            return _context.PropertyInterests.FirstOrDefault(i => i.PropertyInterestId == propertyInterestId);
        }

        public bool Create(PropertyInterest interest)
        {
            _context.PropertyInterests.Add(interest);
            try
            {
                _context.SaveChanges();
                return true;
            }
            catch (DbUpdateException)
            {
                // Exists kontrolünden sonra aynı çift başka bir istekle eklendiyse benzersiz indeks reddeder
                _context.Entry(interest).State = EntityState.Detached;
                if (Exists(interest.PropertyId, interest.ContactId))
                    return false;
                throw;
            }
        }

        public void Delete(PropertyInterest interest)
        {
            _context.PropertyInterests.Remove(interest);
            _context.SaveChanges();
        }
    }
}
