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
            Interests(db, properties, contacts), properties, Requests(db, contacts), Transactions(db, contacts, properties));
    }

    public static TransactionManager Transactions(AppDbContext db, IContactService contacts, IPropertyService properties) =>
        new(new EfTransactionDal(db), contacts, properties);

    // Kontrat deposu verilmezse geçici klasör (dosyaya dokunmayan testler için)
    public static TransactionController Transaction(AppDbContext db, IContactService contacts, IPropertyService properties,
        EmlakOfisi.WebUI.Helpers.DocumentFileStore? documents = null) =>
        new(Transactions(db, contacts, properties), properties, contacts,
            documents ?? new EmlakOfisi.WebUI.Helpers.DocumentFileStore(Path.Combine(Path.GetTempPath(), "emlak-test-docs-" + Guid.NewGuid().ToString("N"))),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionController>.Instance);

    public static CustomerRequestManager Requests(AppDbContext db, IContactService contacts) =>
        new(new EfCustomerRequestDal(db), contacts);

    public static CustomerRequestController Request(AppDbContext db, IContactService contacts) =>
        new(Requests(db, contacts), contacts);

    public static PropertyPhotoManager Photos(AppDbContext db, IPropertyService properties) =>
        new(new EfPropertyPhotoDal(db), properties);

    public static PropertyController Property(AppDbContext db, IPropertyService properties, IContactService contacts) =>
        new(properties, contacts, Interests(db, properties, contacts), Photos(db, properties), Transactions(db, contacts, properties));

    public static AppointmentManager Appointments(AppDbContext db, IContactService contacts, IPropertyService properties) =>
        new(new EfAppointmentDal(db), contacts, properties);

    public static AppointmentController Appointment(AppDbContext db, IContactService contacts, IPropertyService properties) =>
        new(Appointments(db, contacts, properties), contacts, properties);

    public static DashboardController Dashboard(AppDbContext db, IPropertyService properties, IContactService contacts) =>
        new(properties, contacts, Appointments(db, contacts, properties), Transactions(db, contacts, properties));
}
