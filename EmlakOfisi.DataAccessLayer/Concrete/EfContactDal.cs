using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    // InstrumentTrade'den FARK: GenericRepository<Contact>'tan türemez (filtresiz okuma/silme olmasın diye).
    // Sadece Create ve Update'i kendisi yazar.
    public class EfContactDal : IContactDal
    {
        private readonly AppDbContext _context;

        public EfContactDal(AppDbContext context)
        {
            _context = context;
        }

        // GÖRÜNÜRLÜK KURALI (tek yer):
        // silinmemiş VE (yönetici VEYA herkese açık VEYA kaydı oluşturan kişi)
        private IQueryable<Contact> Visible(int userId, bool canSeeAll)
        {
            return _context.Contacts.Where(c => !c.IsDeleted &&
                (canSeeAll || c.Visibility == Visibility.Public || c.CreatedByUserId == userId));
        }

        private IQueryable<Contact> Filtered(int userId, bool canSeeAll, string? nameSearch, string? phoneSearch, ContactRole? role)
        {
            var query = Visible(userId, canSeeAll);

            if (!string.IsNullOrWhiteSpace(phoneSearch))
            {
                query = query.Where(c => c.PhoneNormalized.Contains(phoneSearch));
            }
            else if (!string.IsNullOrWhiteSpace(nameSearch))
            {
                // Büyük/küçük harf duyarsızlığı SQL Server'ın kolon collation'ından gelir (ToLower kullanılmaz:
                // C# tarafı tr-TR kültürüyle "I"→"ı", SQL tarafı "I"→"i" yapıp eşleşmeyi bozuyordu).
                // "Ad Soyad" ve "Soyad Ad" birleşimleri adı ve soyadı tek başına da kapsar.
                var term = nameSearch.Trim();
                query = query.Where(c =>
                    (c.Name + " " + c.Surname).Contains(term) ||
                    (c.Surname + " " + c.Name).Contains(term));
            }

            query = role switch
            {
                ContactRole.Buyer => query.Where(c => c.IsBuyer),
                ContactRole.Seller => query.Where(c => c.IsSeller),
                ContactRole.Tenant => query.Where(c => c.IsTenant),
                ContactRole.Landlord => query.Where(c => c.IsLandlord),
                _ => query
            };

            return query;
        }

        public List<Contact> GetVisibleList(int userId, bool canSeeAll, string? nameSearch, string? phoneSearch,
            ContactRole? role, int skip, int take)
        {
            return Filtered(userId, canSeeAll, nameSearch, phoneSearch, role)
                .AsNoTracking()
                .OrderBy(c => c.Name).ThenBy(c => c.Surname).ThenBy(c => c.ContactId)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public int CountVisible(int userId, bool canSeeAll, string? nameSearch, string? phoneSearch, ContactRole? role)
        {
            return Filtered(userId, canSeeAll, nameSearch, phoneSearch, role).Count();
        }

        public Contact? GetVisibleById(int id, int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll).AsNoTracking().FirstOrDefault(c => c.ContactId == id);
        }

        public Contact? GetVisibleByIdForUpdate(int id, int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll).FirstOrDefault(c => c.ContactId == id);
        }

        public int? FindIdByPhone(string phoneNormalized, int? excludeContactId)
        {
            return _context.Contacts
                .Where(c => !c.IsDeleted && c.PhoneNormalized == phoneNormalized &&
                            (excludeContactId == null || c.ContactId != excludeContactId))
                .OrderBy(c => c.ContactId)
                .Select(c => (int?)c.ContactId)
                .FirstOrDefault();
        }

        public int? FindVisibleIdByPhone(string phoneNormalized, int? excludeContactId, int userId, bool canSeeAll)
        {
            return Visible(userId, canSeeAll)
                .Where(c => c.PhoneNormalized == phoneNormalized &&
                            (excludeContactId == null || c.ContactId != excludeContactId))
                .OrderBy(c => c.ContactId)
                .Select(c => (int?)c.ContactId)
                .FirstOrDefault();
        }

        public void Create(Contact contact)
        {
            _context.Contacts.Add(contact);
            _context.SaveChanges();
        }

        // Kayıt GetVisibleById ile çekilip alanları değiştirildikten sonra çağrılır (takip edilen nesne).
        public void Update(Contact contact)
        {
            // Formdan gelen, takip edilmeyen bir nesne ile tüm kolonların ezilmesini engeller
            if (_context.Entry(contact).State == EntityState.Detached)
                throw new InvalidOperationException("Güncellenecek müşteri önce GetVisibleByIdForUpdate ile okunmalıdır.");

            _context.SaveChanges();
        }
    }
}
