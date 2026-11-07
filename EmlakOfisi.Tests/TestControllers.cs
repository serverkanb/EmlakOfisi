using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.WebUI.Controllers;

namespace EmlakOfisi.Tests;

// Controller'ları gerçek servislerle (InMemory veritabanı) kurmak için ortak yardımcı.
// Bir controller'a yeni servis eklendiğinde testlerde tek yer değişir.
internal static class TestControllers
{
    public static PropertyInterestManager Interests(AppDbContext db, IPropertyService properties, IContactService contacts) =>
        new(new EfPropertyInterestDal(db), properties, contacts);

    public static ContactController Contact(AppDbContext db, IContactService contacts, IActivityService? activities = null)
    {
        var properties = new PropertyManager(new EfPropertyDal(db), contacts);
        return new ContactController(contacts,
            activities ?? new ActivityManager(new EfActivityDal(db), contacts),
            Interests(db, properties, contacts), properties);
    }

    public static PropertyController Property(AppDbContext db, IPropertyService properties, IContactService contacts) =>
        new(properties, contacts, Interests(db, properties, contacts));
}
