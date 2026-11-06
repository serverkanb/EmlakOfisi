using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.BusinessLayer.Concrete
{
    public class ActivityManager : IActivityService
    {
        private readonly IActivityDal _activityDal;
        private readonly IContactService _contactService;

        // Müşteri görünürlüğü ve düzenleme kuralı tek yerde kalsın diye IContactService kullanılır
        public ActivityManager(IActivityDal activityDal, IContactService contactService)
        {
            _activityDal = activityDal;
            _contactService = contactService;
        }

        public List<Activity>? TGetForContact(int contactId, int userId, bool canSeeAll)
        {
            if (_contactService.TGetVisibleById(contactId, userId, canSeeAll) == null)
                return null;

            return _activityDal.GetListByContact(contactId);
        }

        public bool TCanAdd(Contact contact, int userId, bool canSeeAll)
        {
            return _contactService.TCanEdit(contact, userId, canSeeAll);
        }

        public bool TCanDelete(Activity activity, int userId, bool canSeeAll)
        {
            return canSeeAll || activity.UserId == userId;
        }

        public ContactOperationResult TCheckAdd(int contactId, int userId, bool canSeeAll)
        {
            var contact = _contactService.TGetVisibleById(contactId, userId, canSeeAll);
            if (contact == null)
                return ContactOperationResult.NotFound;
            return TCanAdd(contact, userId, canSeeAll) ? ContactOperationResult.Success : ContactOperationResult.Forbidden;
        }

        public ContactOperationResult TAdd(int contactId, Activity activity, int userId, bool canSeeAll)
        {
            var check = TCheckAdd(contactId, userId, canSeeAll);
            if (check != ContactOperationResult.Success)
                return check;

            activity.ActivityId = 0;
            activity.ContactId = contactId;
            activity.UserId = userId;
            activity.PropertyId = null; // portföy bağlantısı görev 12'de
            activity.CreatedAt = DateTime.Now;
            if (!Enum.IsDefined(activity.Type))
                activity.Type = ActivityType.Note;

            _activityDal.Create(activity);
            return ContactOperationResult.Success;
        }

        public ContactOperationResult TDelete(int activityId, int userId, bool canSeeAll, out int contactId)
        {
            contactId = 0;
            var activity = _activityDal.GetByIdForUpdate(activityId);
            if (activity == null)
                return ContactOperationResult.NotFound;

            // Bağlı müşteriyi göremeyen, notun varlığını da öğrenemez
            if (_contactService.TGetVisibleById(activity.ContactId, userId, canSeeAll) == null)
                return ContactOperationResult.NotFound;

            if (!TCanDelete(activity, userId, canSeeAll))
                return ContactOperationResult.Forbidden;

            contactId = activity.ContactId;
            _activityDal.Delete(activity);
            return ContactOperationResult.Success;
        }
    }
}
