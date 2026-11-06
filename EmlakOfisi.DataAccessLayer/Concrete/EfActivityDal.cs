using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Concrete
{
    public class EfActivityDal : IActivityDal
    {
        public const int MaxItems = 200;

        private readonly AppDbContext _context;

        public EfActivityDal(AppDbContext context)
        {
            _context = context;
        }

        public List<Activity> GetListByContact(int contactId)
        {
            return _context.Activities
                .AsNoTracking()
                .Include(a => a.User)
                .Where(a => a.ContactId == contactId)
                .OrderByDescending(a => a.ActivityDate).ThenByDescending(a => a.ActivityId)
                .Take(MaxItems) // detay sayfası ağırlaşmasın; "tümünü gör" gerekirse sonra eklenir
                .ToList();
        }

        public Activity? GetByIdForUpdate(int activityId)
        {
            return _context.Activities.FirstOrDefault(a => a.ActivityId == activityId);
        }

        public void Create(Activity activity)
        {
            _context.Activities.Add(activity);
            _context.SaveChanges();
        }

        public void Delete(Activity activity)
        {
            _context.Activities.Remove(activity);
            _context.SaveChanges();
        }
    }
}
