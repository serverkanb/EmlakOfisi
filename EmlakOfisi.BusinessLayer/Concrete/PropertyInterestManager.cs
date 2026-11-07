using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;

namespace EmlakOfisi.BusinessLayer.Concrete
{
    public class PropertyInterestManager : IPropertyInterestService
    {
        private readonly IPropertyInterestDal _interestDal;
        private readonly IPropertyService _propertyService;
        private readonly IContactService _contactService;

        // Portföy ve müşteri görünürlüğü/düzenleme kuralları kendi servislerinde kalsın diye onlar kullanılır
        public PropertyInterestManager(IPropertyInterestDal interestDal, IPropertyService propertyService, IContactService contactService)
        {
            _interestDal = interestDal;
            _propertyService = propertyService;
            _contactService = contactService;
        }

        public List<PropertyInterest>? TGetForProperty(int propertyId, int userId, bool canSeeAll)
        {
            if (_propertyService.TGetVisibleById(propertyId, userId, canSeeAll) == null)
                return null;

            return _interestDal.GetListByProperty(propertyId, userId, canSeeAll);
        }

        public List<PropertyInterest>? TGetForContact(int contactId, int userId, bool canSeeAll)
        {
            if (_contactService.TGetVisibleById(contactId, userId, canSeeAll) == null)
                return null;

            return _interestDal.GetListByContact(contactId, userId, canSeeAll);
        }

        public bool TCanDelete(PropertyInterest interest, Property property, int userId, bool canSeeAll)
        {
            return canSeeAll || interest.CreatedByUserId == userId || _propertyService.TCanEdit(property, userId, canSeeAll);
        }

        public OperationResult TAdd(int propertyId, int contactId, string? note, int userId, bool canSeeAll)
        {
            // Önce görünürlük: gizli portföye ya da gizli müşteriye bağlantı kurulamaz, varlıkları da belli olmaz
            if (_propertyService.TGetVisibleById(propertyId, userId, canSeeAll) == null)
                return OperationResult.NotFound;
            if (_contactService.TGetVisibleById(contactId, userId, canSeeAll) == null)
                return OperationResult.NotFound;

            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note?.Length > IPropertyInterestService.MaxNoteLength)
                return OperationResult.Invalid;

            if (_interestDal.Exists(propertyId, contactId))
                return OperationResult.Invalid;

            var created = _interestDal.Create(new PropertyInterest
            {
                PropertyId = propertyId,
                ContactId = contactId,
                Note = note,
                CreatedByUserId = userId,
                CreatedAt = DateTime.Now
            });
            return created ? OperationResult.Success : OperationResult.Invalid;
        }

        public OperationResult TDelete(int propertyInterestId, int userId, bool canSeeAll, out int propertyId, out int contactId)
        {
            propertyId = 0;
            contactId = 0;

            var interest = _interestDal.GetByIdForUpdate(propertyInterestId);
            if (interest == null)
                return OperationResult.NotFound;

            // Portföyü ya da müşteriyi göremeyen, satırın varlığını da öğrenemez
            var property = _propertyService.TGetVisibleById(interest.PropertyId, userId, canSeeAll);
            if (property == null || _contactService.TGetVisibleById(interest.ContactId, userId, canSeeAll) == null)
                return OperationResult.NotFound;

            if (!TCanDelete(interest, property, userId, canSeeAll))
                return OperationResult.Forbidden;

            propertyId = interest.PropertyId;
            contactId = interest.ContactId;
            _interestDal.Delete(interest);
            return OperationResult.Success;
        }
    }
}
