using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Concrete
{
    public class PropertyManager : IPropertyService
    {
        private readonly IPropertyDal _propertyDal;
        private readonly IContactService _contactService;

        // Mülk sahibinin görünürlüğü müşteri kuralıyla kontrol edilir (tek yer: IContactService)
        public PropertyManager(IPropertyDal propertyDal, IContactService contactService)
        {
            _propertyDal = propertyDal;
            _contactService = contactService;
        }

        public (List<Property> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            PropertyFilter filter, int page, int pageSize)
        {
            page = Math.Clamp(page, 1, 100_000);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var items = _propertyDal.GetVisibleList(userId, canSeeAll, filter, (page - 1) * pageSize, pageSize);
            var total = _propertyDal.CountVisible(userId, canSeeAll, filter);
            return (items, total);
        }

        public Property? TGetVisibleById(int id, int userId, bool canSeeAll)
        {
            return _propertyDal.GetVisibleById(id, userId, canSeeAll);
        }

        public PropertyCounts TGetVisibleCounts(int userId, bool canSeeAll)
        {
            var counts = new PropertyCounts();
            foreach (var (status, listingType, count) in _propertyDal.CountVisibleByStatus(userId, canSeeAll))
            {
                switch (status)
                {
                    case PropertyStatus.Active:
                        counts.Active += count;
                        if (listingType == ListingType.Sale) counts.ActiveSale += count;
                        else if (listingType == ListingType.Rent) counts.ActiveRent += count;
                        break;
                    case PropertyStatus.Passive: counts.Passive += count; break;
                    case PropertyStatus.Sold: counts.Sold += count; break;
                    case PropertyStatus.Rented: counts.Rented += count; break;
                }
            }
            return counts;
        }

        public bool TCanEdit(Property property, int userId, bool canSeeAll)
        {
            return !property.IsDeleted &&
                   (canSeeAll || property.CreatedByUserId == userId || property.AgentUserId == userId);
        }

        public bool TCanDelete(Property property, int userId, bool canSeeAll)
        {
            return !property.IsDeleted && (canSeeAll || property.CreatedByUserId == userId);
        }

        public Contact? TGetVisibleOwner(Property property, int userId, bool canSeeAll)
        {
            return property.OwnerContactId == null
                ? null
                : _contactService.TGetVisibleById(property.OwnerContactId.Value, userId, canSeeAll);
        }

        public List<(int Id, string FullName)> TGetAssignableAgents()
        {
            return _propertyDal.GetActiveStaff();
        }

        public OperationResult TCreate(Property property, int userId, bool canSeeAll)
        {
            if (!IsValid(property))
                return OperationResult.Invalid;

            // Mülk sahibi kullanıcının görebildiği bir kişi olmalı (gizli müşteri id'si yazılamasın)
            if (property.OwnerContactId != null &&
                _contactService.TGetVisibleById(property.OwnerContactId.Value, userId, canSeeAll) == null)
                return OperationResult.Invalid;

            property.PropertyId = 0;
            property.CreatedByUserId = userId;
            property.CreatedAt = DateTime.Now;
            property.UpdatedAt = null;
            property.IsDeleted = false;

            // Danışman başkasını sorumlu yapamaz; yönetici seçmezse sorumlu kendisidir.
            // Yöneticinin seçtiği kişi aktif bir ofis çalışanı olmalı (pasif/rolsüz/olmayan kullanıcı olamaz).
            if (!canSeeAll || property.AgentUserId == 0)
                property.AgentUserId = userId;
            else if (property.AgentUserId != userId && !_propertyDal.IsActiveStaff(property.AgentUserId))
                return OperationResult.Invalid;

            NormalizeEnums(property);
            _propertyDal.Create(property);
            return OperationResult.Success;
        }

        public OperationResult TUpdate(int id, Property changes, int userId, bool canSeeAll)
        {
            var property = _propertyDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (property == null)
                return OperationResult.NotFound;
            if (!TCanEdit(property, userId, canSeeAll))
                return OperationResult.Forbidden;
            if (!IsValid(changes))
                return OperationResult.Invalid;

            // Mülk sahibi kuralı:
            // - Mevcut sahibi GÖREMEYEN kullanıcı (ör. sorumlu danışman) sahibi değiştiremez/kaldıramaz; gönderdiği
            //   değer yok sayılır, mevcut sahip korunur. (Formda gizli kişinin id'si taşınmaz, bağlantı da kaybolmaz.)
            // - Görebiliyorsa yeni sahip de görebildiği bir kişi olmalı.
            var currentOwnerHidden = property.OwnerContactId != null &&
                _contactService.TGetVisibleById(property.OwnerContactId.Value, userId, canSeeAll) == null;
            var newOwnerId = currentOwnerHidden ? property.OwnerContactId : changes.OwnerContactId;
            if (!currentOwnerHidden && newOwnerId != null && newOwnerId != property.OwnerContactId &&
                _contactService.TGetVisibleById(newOwnerId.Value, userId, canSeeAll) == null)
                return OperationResult.Invalid;

            // Sorumlu danışmanı sadece yönetici değiştirir; yeni sorumlu aktif bir ofis çalışanı olmalı
            var newAgentId = property.AgentUserId;
            if (canSeeAll && changes.AgentUserId != 0 && changes.AgentUserId != property.AgentUserId)
            {
                if (!_propertyDal.IsActiveStaff(changes.AgentUserId))
                    return OperationResult.Invalid;
                newAgentId = changes.AgentUserId;
            }

            // Sadece izinli alanlar. CreatedByUserId, CreatedAt, IsDeleted değişmez.
            property.Title = changes.Title;
            property.ListingType = changes.ListingType;
            property.PropertyType = changes.PropertyType;
            property.Status = changes.Status;
            property.Price = changes.Price;
            property.City = changes.City;
            property.District = changes.District;
            property.Neighborhood = changes.Neighborhood;
            property.Address = changes.Address;
            property.Latitude = changes.Latitude;
            property.Longitude = changes.Longitude;
            property.GrossArea = changes.GrossArea;
            property.NetArea = changes.NetArea;
            property.RoomCount = changes.RoomCount;
            property.Floor = changes.Floor;
            property.BuildingAge = changes.BuildingAge;
            property.Description = changes.Description;
            property.ListingUrl = changes.ListingUrl;
            property.OwnerContactId = newOwnerId;
            property.AgentUserId = newAgentId;
            // Kullanıcı kararı (görev 11): düzenleyebilen herkes (sorumlu danışman dahil) görünürlüğü değiştirebilir
            property.Visibility = Enum.IsDefined(changes.Visibility) ? changes.Visibility : property.Visibility;

            NormalizeEnums(property);
            property.UpdatedAt = DateTime.Now;
            _propertyDal.Update(property);
            return OperationResult.Success;
        }

        public OperationResult TSetStatus(int id, PropertyStatus status, int userId, bool canSeeAll)
        {
            var property = _propertyDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (property == null)
                return OperationResult.NotFound;
            if (!TCanEdit(property, userId, canSeeAll))
                return OperationResult.Forbidden;
            if (!Enum.IsDefined(status))
                return OperationResult.Invalid;

            property.Status = status;
            property.UpdatedAt = DateTime.Now;
            _propertyDal.Update(property);
            return OperationResult.Success;
        }

        public OperationResult TSoftDelete(int id, int userId, bool canSeeAll)
        {
            var property = _propertyDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (property == null)
                return OperationResult.NotFound;
            if (!TCanDelete(property, userId, canSeeAll))
                return OperationResult.Forbidden;

            property.IsDeleted = true;
            property.UpdatedAt = DateTime.Now;
            _propertyDal.Update(property);
            return OperationResult.Success;
        }

        // Fiyat ve alanlar negatif olamaz, koordinatlar geçerli aralıkta olmalı
        private static bool IsValid(Property p)
        {
            if (p.Price < 0) return false;
            if (p.GrossArea < 0 || p.NetArea < 0 || p.BuildingAge < 0) return false;
            if (p.Latitude is < -90 or > 90) return false;
            if (p.Longitude is < -180 or > 180) return false;
            if (p.ListingUrl != null && !UrlValidator.IsSafeHttpUrl(p.ListingUrl)) return false;
            return true;
        }

        // Geçersiz enum değeri gelirse güvenli varsayılana düşer (görünürlük: Sadece Ben)
        private static void NormalizeEnums(Property p)
        {
            if (!Enum.IsDefined(p.Visibility)) p.Visibility = Visibility.OnlyMe;
            if (!Enum.IsDefined(p.ListingType)) p.ListingType = ListingType.Sale;
            if (!Enum.IsDefined(p.PropertyType)) p.PropertyType = PropertyType.Other;
            if (!Enum.IsDefined(p.Status)) p.Status = PropertyStatus.Active;

            // Arsada oda sayısı, kat ve bina yaşı anlamsız: formdan gelse de kaydedilmez (alan m² kullanılır)
            if (p.PropertyType == PropertyType.Land)
            {
                p.RoomCount = null;
                p.Floor = null;
                p.BuildingAge = null;
            }
        }
    }
}
