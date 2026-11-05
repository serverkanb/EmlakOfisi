using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Concrete
{
    public class ContactManager : IContactService
    {
        private readonly IContactDal _contactDal;

        public ContactManager(IContactDal contactDal)
        {
            _contactDal = contactDal;
        }

        public (List<Contact> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            string? search, ContactRole? role, int page, int pageSize)
        {
            string? nameSearch = null;
            string? phoneSearch = null;

            if (!string.IsNullOrWhiteSpace(search))
            {
                if (PhoneNormalizer.LooksLikePhone(search))
                    phoneSearch = PhoneNormalizer.NormalizeForSearch(search);
                else
                    nameSearch = search.Trim();
            }

            if (page < 1) page = 1;
            pageSize = Math.Clamp(pageSize, 1, 100); // adresten çok büyük değer gelirse tüm tablo çekilmesin

            var items = _contactDal.GetVisibleList(userId, canSeeAll, nameSearch, phoneSearch, role,
                (page - 1) * pageSize, pageSize);
            var total = _contactDal.CountVisible(userId, canSeeAll, nameSearch, phoneSearch, role);

            return (items, total);
        }

        public Contact? TGetVisibleById(int id, int userId, bool canSeeAll)
        {
            return _contactDal.GetVisibleById(id, userId, canSeeAll);
        }

        // Düzenleme: yönetici veya kaydı oluşturan kişi
        public bool TCanEdit(Contact contact, int userId, bool canSeeAll)
        {
            return !contact.IsDeleted && (canSeeAll || contact.CreatedByUserId == userId);
        }

        // Silme: yönetici veya kaydı oluşturan kişi (kural ileride ayrışabilir diye ayrı metot)
        public bool TCanDelete(Contact contact, int userId, bool canSeeAll)
        {
            return !contact.IsDeleted && (canSeeAll || contact.CreatedByUserId == userId);
        }

        public void TCreate(Contact contact, int userId)
        {
            contact.ContactId = 0;
            contact.CreatedByUserId = userId;
            contact.CreatedAt = DateTime.Now;
            contact.UpdatedAt = null;
            contact.IsDeleted = false;
            contact.PhoneNormalized = PhoneNormalizer.Normalize(contact.Phone);
            if (!Enum.IsDefined(contact.Visibility))
                contact.Visibility = Visibility.OnlyMe; // geçersiz değer gelirse güvenli taraf
            _contactDal.Create(contact);
        }

        public ContactOperationResult TUpdate(int id, Contact changes, int userId, bool canSeeAll)
        {
            var contact = _contactDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (contact == null)
                return ContactOperationResult.NotFound;
            if (!TCanEdit(contact, userId, canSeeAll))
                return ContactOperationResult.Forbidden;

            // Sadece izin verilen alanlar. CreatedByUserId, CreatedAt, IsDeleted değiştirilemez.
            contact.Name = changes.Name;
            contact.Surname = changes.Surname;
            contact.Phone = changes.Phone;
            contact.PhoneNormalized = PhoneNormalizer.Normalize(changes.Phone);
            contact.Phone2 = changes.Phone2;
            contact.Email = changes.Email;
            contact.Notes = changes.Notes;
            contact.IsBuyer = changes.IsBuyer;
            contact.IsSeller = changes.IsSeller;
            contact.IsTenant = changes.IsTenant;
            contact.IsLandlord = changes.IsLandlord;
            if (Enum.IsDefined(changes.Visibility))
                contact.Visibility = changes.Visibility;
            contact.UpdatedAt = DateTime.Now;

            _contactDal.Update(contact);
            return ContactOperationResult.Success;
        }

        public ContactOperationResult TSoftDelete(int id, int userId, bool canSeeAll)
        {
            var contact = _contactDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (contact == null)
                return ContactOperationResult.NotFound;
            if (!TCanDelete(contact, userId, canSeeAll))
                return ContactOperationResult.Forbidden;

            contact.IsDeleted = true;
            contact.UpdatedAt = DateTime.Now;
            _contactDal.Update(contact);
            return ContactOperationResult.Success;
        }

        public DuplicatePhoneResult TCheckDuplicatePhone(string phone, int userId, bool canSeeAll, int? excludeContactId = null)
        {
            var normalized = PhoneNormalizer.Normalize(phone);
            if (normalized.Length == 0)
                return new DuplicatePhoneResult();

            var existingId = _contactDal.FindIdByPhone(normalized, excludeContactId);
            if (existingId == null)
                return new DuplicatePhoneResult();

            // Var. Kullanıcı o kaydı görebiliyorsa id'sini ver (link gösterilsin), göremiyorsa verme.
            var visible = _contactDal.GetVisibleById(existingId.Value, userId, canSeeAll) != null;
            return new DuplicatePhoneResult
            {
                Exists = true,
                VisibleContactId = visible ? existingId : null
            };
        }
    }
}
