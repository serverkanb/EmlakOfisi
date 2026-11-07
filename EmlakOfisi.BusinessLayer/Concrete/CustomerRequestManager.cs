using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Concrete
{
    public class CustomerRequestManager : ICustomerRequestService
    {
        private readonly ICustomerRequestDal _requestDal;
        private readonly IContactService _contactService;

        // Müşteri görünürlüğü ve düzenleme kuralı tek yerde kalsın diye IContactService kullanılır
        public CustomerRequestManager(ICustomerRequestDal requestDal, IContactService contactService)
        {
            _requestDal = requestDal;
            _contactService = contactService;
        }

        public (List<CustomerRequestRow> Items, int TotalCount) TGetVisibleList(int userId, bool canSeeAll,
            CustomerRequestFilter filter, int page, int pageSize)
        {
            page = Math.Clamp(page, 1, 100_000);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var items = _requestDal.GetVisibleList(userId, canSeeAll, filter, (page - 1) * pageSize, pageSize);
            var total = _requestDal.CountVisible(userId, canSeeAll, filter);
            return (items, total);
        }

        public CustomerRequest? TGetVisibleById(int id, int userId, bool canSeeAll)
        {
            return _requestDal.GetVisibleById(id, userId, canSeeAll);
        }

        public bool TCanEdit(Contact contact, int userId, bool canSeeAll, bool canEditOthers)
        {
            return !contact.IsDeleted && (canEditOthers || _contactService.TCanEdit(contact, userId, canSeeAll));
        }

        public OperationResult TCheckAdd(int contactId, int userId, bool canSeeAll, bool canEditOthers)
        {
            var contact = _contactService.TGetVisibleById(contactId, userId, canSeeAll);
            if (contact == null)
                return OperationResult.NotFound;
            return TCanEdit(contact, userId, canSeeAll, canEditOthers) ? OperationResult.Success : OperationResult.Forbidden;
        }

        public OperationResult TCreate(int contactId, CustomerRequest request, int userId, bool canSeeAll, bool canEditOthers)
        {
            var check = TCheckAdd(contactId, userId, canSeeAll, canEditOthers);
            if (check != OperationResult.Success)
                return check;
            if (!IsValid(request))
                return OperationResult.Invalid;

            request.CustomerRequestId = 0;
            request.ContactId = contactId;
            request.Contact = null;
            request.CreatedByUserId = userId;
            request.CreatedByUser = null;
            request.CreatedAt = DateTime.Now;
            request.Status = RequestStatus.Open;
            Sanitize(request);

            _requestDal.Create(request);
            return OperationResult.Success;
        }

        public OperationResult TUpdate(int id, CustomerRequest changes, int userId, bool canSeeAll, bool canEditOthers)
        {
            var request = _requestDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (request == null)
                return OperationResult.NotFound;
            var check = TCheckAdd(request.ContactId, userId, canSeeAll, canEditOthers);
            if (check != OperationResult.Success)
                return check;
            if (!IsValid(changes))
                return OperationResult.Invalid;

            Sanitize(changes);
            request.ListingType = changes.ListingType;
            request.PropertyType = changes.PropertyType;
            request.City = changes.City;
            request.District = changes.District;
            request.MinPrice = changes.MinPrice;
            request.MaxPrice = changes.MaxPrice;
            request.RoomCount = changes.RoomCount;
            request.Notes = changes.Notes;

            _requestDal.Update(request);
            return OperationResult.Success;
        }

        public OperationResult TSetStatus(int id, RequestStatus status, int userId, bool canSeeAll, bool canEditOthers, out int contactId)
        {
            contactId = 0;
            var request = _requestDal.GetVisibleByIdForUpdate(id, userId, canSeeAll);
            if (request == null)
                return OperationResult.NotFound;
            var check = TCheckAdd(request.ContactId, userId, canSeeAll, canEditOthers);
            if (check != OperationResult.Success)
                return check;
            if (!Enum.IsDefined(status))
                return OperationResult.Invalid;

            contactId = request.ContactId;
            request.Status = status;
            _requestDal.Update(request);
            return OperationResult.Success;
        }

        private static bool IsValid(CustomerRequest r) =>
            (r.MinPrice == null || r.MinPrice >= 0) &&
            (r.MaxPrice == null || r.MaxPrice >= 0) &&
            (r.MinPrice == null || r.MaxPrice == null || r.MinPrice <= r.MaxPrice) &&
            (r.Notes == null || r.Notes.Length <= ICustomerRequestService.MaxNotesLength);

        // Geçersiz enum güvenli varsayılana düşer
        private static void Sanitize(CustomerRequest r)
        {
            if (!Enum.IsDefined(r.ListingType)) r.ListingType = ListingType.Sale;
            if (r.PropertyType != null && !Enum.IsDefined(r.PropertyType.Value)) r.PropertyType = null;
        }
    }
}
