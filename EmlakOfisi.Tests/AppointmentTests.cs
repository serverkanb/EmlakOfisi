using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Görev 14: randevular. Ayşe, Bora, Cem: danışman. Mert: yönetici.
// Görme/düzenleme: yönetici, sorumlu ya da oluşturan. Bağlı müşteri/portföy görüntüleyene görünmüyorsa bilgisi verilmez.
public class AppointmentTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public PropertyManager Properties { get; private init; } = null!;
        public AppointmentManager Appointments { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
        public AppUser Cem { get; private init; } = null!;
        public AppUser Mert { get; private init; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var contacts = new ContactManager(new EfContactDal(identity.Db));
            var properties = new PropertyManager(new EfPropertyDal(identity.Db), contacts);
            return new Fixture
            {
                Identity = identity,
                Contacts = contacts,
                Properties = properties,
                Appointments = TestControllers.Appointments(identity.Db, contacts, properties),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Cem = await identity.AddUserAsync("cem", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        public bool IsManager(AppUser u) => u == Mert;
        public string RoleOf(AppUser u) => u == Mert ? RoleNames.Manager : RoleNames.Agent;

        public AppointmentController As(AppUser user) =>
            Identity.SignedInAs(TestControllers.Appointment(Identity.Db, Contacts, Properties), user, RoleOf(user));

        private int _phone;
        public Contact AddContact(AppUser owner, Visibility visibility, string name = "Kişi")
        {
            var c = new Contact { Name = name, Surname = "Test", Phone = $"0100{++_phone:D7}", Visibility = visibility, IsBuyer = true };
            Contacts.TCreate(c, owner.Id);
            return c;
        }

        public Property AddProperty(AppUser creator, Visibility visibility, string title = "Portföy", int agent = 0)
        {
            var p = new Property
            {
                Title = title, City = "İstanbul", District = "Kadıköy", Price = 1_000_000, Visibility = visibility,
                AgentUserId = agent, Address = "Moda Cad. 1"
            };
            Assert.Equal(OperationResult.Success, Properties.TCreate(p, creator.Id, IsManager(creator)));
            return p;
        }

        public Appointment Add(AppUser creator, DateTime? start = null, int? contactId = null, int? propertyId = null,
            int agent = 0, string? notes = null)
        {
            var a = new Appointment
            {
                StartAt = start ?? DateTime.Today.AddDays(1).AddHours(10),
                ContactId = contactId, PropertyId = propertyId, AgentUserId = agent, Notes = notes
            };
            Assert.Equal(OperationResult.Success, Appointments.TCreate(a, creator.Id, IsManager(creator)));
            return a;
        }

        public List<AppointmentRow> List(AppUser user, AppointmentScope? scope = null, bool mine = false) =>
            Appointments.TGetVisibleList(user.Id, IsManager(user), new AppointmentFilter { Scope = scope, OnlyMine = mine }, 1, 100).Items;

        public Appointment Read(int id) => Appointments.TGetVisibleById(id, Mert.Id, true)!.Appointment;

        public void Dispose() => Identity.Dispose();
    }

    private static T ModelOf<T>(IActionResult r) => Assert.IsType<T>(Assert.IsType<ViewResult>(r).Model);

    private static AppointmentFormViewModel Form(DateTime? start = null, int? contactId = null, int? propertyId = null,
        int agent = 0, string? notes = "Not") => new()
    {
        Type = AppointmentType.Meeting,
        StartAt = start ?? DateTime.Today.AddDays(2).AddHours(9),
        ContactId = contactId, PropertyId = propertyId, AgentUserId = agent, Notes = notes
    };

    // ---------------- Görünürlük ----------------

    [Fact]
    public async Task Visibility_AgentSeesOwnAndAssigned_ManagerSeesAll()
    {
        using var f = await Fixture.CreateAsync();
        var ayse = f.Add(f.Ayse);
        var bora = f.Add(f.Bora);
        var assignedToCem = f.Add(f.Mert, agent: f.Cem.Id);
        var mertOwn = f.Add(f.Mert);

        Assert.Equal(new[] { ayse.AppointmentId }, f.List(f.Ayse).Select(r => r.Appointment.AppointmentId));
        Assert.Equal(new[] { bora.AppointmentId }, f.List(f.Bora).Select(r => r.Appointment.AppointmentId));
        Assert.Equal(new[] { assignedToCem.AppointmentId }, f.List(f.Cem).Select(r => r.Appointment.AppointmentId));
        Assert.Equal(4, f.List(f.Mert).Count);
        Assert.Equal(new[] { mertOwn.AppointmentId }, f.List(f.Mert, mine: true).Select(r => r.Appointment.AppointmentId));
    }

    [Fact]
    public async Task CreatorKeepsAccess_AfterAssigningToAnother()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Mert, agent: f.Ayse.Id);
        // Danışman başkası adına ekleyemez ama yönetici ekleyip atadığında oluşturan (yönetici) zaten her şeyi görür;
        // danışman kendi eklediğinin sorumlusu değişse de oluşturan olarak görür:
        var b = f.Add(f.Bora);
        Assert.Equal(OperationResult.Success, f.Appointments.TUpdate(b.AppointmentId,
            new Appointment { StartAt = b.StartAt, AgentUserId = f.Cem.Id, Type = b.Type, Status = b.Status }, f.Mert.Id, true));

        Assert.Contains(f.List(f.Bora), r => r.Appointment.AppointmentId == b.AppointmentId);
        Assert.Contains(f.List(f.Cem), r => r.Appointment.AppointmentId == b.AppointmentId);
        Assert.Contains(f.List(f.Ayse), r => r.Appointment.AppointmentId == a.AppointmentId);
    }

    [Fact]
    public async Task Detail_NotVisible_ReturnsNull()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Ayse);
        Assert.Null(f.Appointments.TGetVisibleById(a.AppointmentId, f.Bora.Id, false));
        Assert.NotNull(f.Appointments.TGetVisibleById(a.AppointmentId, f.Mert.Id, true));
    }

    [Fact]
    public async Task Scopes_UpcomingPastCancelled()
    {
        using var f = await Fixture.CreateAsync();
        var future = f.Add(f.Ayse, DateTime.Today.AddDays(3).AddHours(9));
        var soon = f.Add(f.Ayse, DateTime.Today.AddDays(1).AddHours(9));
        var past = f.Add(f.Ayse, DateTime.Today.AddDays(-1).AddHours(9));
        var doneFuture = f.Add(f.Ayse, DateTime.Today.AddDays(2).AddHours(9));
        var cancelled = f.Add(f.Ayse, DateTime.Today.AddDays(4).AddHours(9));
        f.Appointments.TSetStatus(doneFuture.AppointmentId, AppointmentStatus.Done, f.Ayse.Id, false);
        f.Appointments.TSetStatus(cancelled.AppointmentId, AppointmentStatus.Cancelled, f.Ayse.Id, false);

        Assert.Equal(new[] { soon.AppointmentId, future.AppointmentId },
            f.List(f.Ayse, AppointmentScope.Upcoming).Select(r => r.Appointment.AppointmentId)); // en yakın üstte
        Assert.Equal(new[] { past.AppointmentId, doneFuture.AppointmentId }.OrderBy(x => x),
            f.List(f.Ayse, AppointmentScope.Past).Select(r => r.Appointment.AppointmentId).OrderBy(x => x));
        Assert.Equal(new[] { cancelled.AppointmentId },
            f.List(f.Ayse, AppointmentScope.Cancelled).Select(r => r.Appointment.AppointmentId));
    }

    // ---------------- Bağlı kayıtlar ----------------

    [Fact]
    public async Task Create_WithHiddenContactOrProperty_IsInvalid()
    {
        using var f = await Fixture.CreateAsync();
        var hiddenContact = f.AddContact(f.Bora, Visibility.OnlyMe);
        var hiddenProperty = f.AddProperty(f.Bora, Visibility.OnlyMe);

        Assert.Equal(OperationResult.Invalid, f.Appointments.TCreate(
            new Appointment { StartAt = DateTime.Today.AddDays(1), ContactId = hiddenContact.ContactId }, f.Ayse.Id, false));
        Assert.Equal(OperationResult.Invalid, f.Appointments.TCreate(
            new Appointment { StartAt = DateTime.Today.AddDays(1), PropertyId = hiddenProperty.PropertyId }, f.Ayse.Id, false));
        Assert.Empty(f.List(f.Mert));
    }

    [Fact]
    public async Task LinkedRecord_HiddenFromViewer_IsNotLeaked()
    {
        using var f = await Fixture.CreateAsync();
        var contact = f.AddContact(f.Mert, Visibility.OnlyMe, "Gizli");
        var property = f.AddProperty(f.Mert, Visibility.OnlyMe, "Gizli Portföy");
        // Yönetici, kendi gizli kayıtlarına bağlı bir randevuyu Ayşe'ye atar
        var a = f.Add(f.Mert, contactId: contact.ContactId, propertyId: property.PropertyId, agent: f.Ayse.Id);

        var row = Assert.Single(f.List(f.Ayse));
        Assert.Null(row.Contact);
        Assert.Null(row.Property);
        Assert.True(row.ContactHidden);
        Assert.True(row.PropertyHidden);

        var item = AppointmentItemViewModel.From(row, f.Ayse.Id, true);
        Assert.Null(item.ContactId);
        Assert.Null(item.ContactName);
        Assert.Equal(string.Empty, item.TelLink);
        Assert.Null(item.PropertyId);
        Assert.Null(item.PropertyTitle);
        Assert.Null(item.MapLink);
        Assert.Equal("Gizli kayıt · Gizli kayıt", item.LinkSummary);

        // Yönetici bağlı kayıtları görür
        var forMert = Assert.Single(f.List(f.Mert));
        Assert.Equal(contact.ContactId, forMert.Contact!.ContactId);
        Assert.Equal(property.PropertyId, forMert.Property!.PropertyId);
        _ = a;
    }

    [Fact]
    public async Task DeletedContact_IsShownAsHidden()
    {
        using var f = await Fixture.CreateAsync();
        var contact = f.AddContact(f.Ayse, Visibility.Public);
        f.Add(f.Ayse, contactId: contact.ContactId);
        f.Contacts.TSoftDelete(contact.ContactId, f.Ayse.Id, false);

        Assert.True(Assert.Single(f.List(f.Ayse)).ContactHidden);
    }

    [Fact]
    public async Task Update_HiddenLinkIsPreserved_AndCannotBeRemoved()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Mert, Visibility.OnlyMe);
        var a = f.Add(f.Mert, contactId: hidden.ContactId, agent: f.Ayse.Id);
        var visible = f.AddContact(f.Ayse, Visibility.OnlyMe);

        // Ayşe gizli bağlantıyı kaldırmaya ya da değiştirmeye çalışır: yok sayılır
        Assert.Equal(OperationResult.Success, f.Appointments.TUpdate(a.AppointmentId,
            new Appointment { StartAt = a.StartAt, ContactId = null, Type = a.Type, Status = a.Status }, f.Ayse.Id, false));
        Assert.Equal(hidden.ContactId, f.Read(a.AppointmentId).ContactId);

        Assert.Equal(OperationResult.Success, f.Appointments.TUpdate(a.AppointmentId,
            new Appointment { StartAt = a.StartAt, ContactId = visible.ContactId, Type = a.Type, Status = a.Status }, f.Ayse.Id, false));
        Assert.Equal(hidden.ContactId, f.Read(a.AppointmentId).ContactId);
    }

    [Fact]
    public async Task Update_ToHiddenContact_IsInvalid_VisibleChangeWorks()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Ayse);
        var bora = f.AddContact(f.Bora, Visibility.OnlyMe);
        var own = f.AddContact(f.Ayse, Visibility.OnlyMe);

        Assert.Equal(OperationResult.Invalid, f.Appointments.TUpdate(a.AppointmentId,
            new Appointment { StartAt = a.StartAt, ContactId = bora.ContactId, Type = a.Type, Status = a.Status }, f.Ayse.Id, false));
        Assert.Equal(OperationResult.Success, f.Appointments.TUpdate(a.AppointmentId,
            new Appointment { StartAt = a.StartAt, ContactId = own.ContactId, Type = a.Type, Status = a.Status }, f.Ayse.Id, false));
        Assert.Equal(own.ContactId, f.Read(a.AppointmentId).ContactId);
    }

    // ---------------- Sorumlu danışman ----------------

    [Fact]
    public async Task Agent_CannotCreateForAnother_ManagerCan()
    {
        using var f = await Fixture.CreateAsync();
        var byAgent = f.Add(f.Ayse, agent: f.Bora.Id);
        Assert.Equal(f.Ayse.Id, f.Read(byAgent.AppointmentId).AgentUserId);

        var byManager = f.Add(f.Mert, agent: f.Bora.Id);
        Assert.Equal(f.Bora.Id, f.Read(byManager.AppointmentId).AgentUserId);
        Assert.Equal(f.Mert.Id, f.Read(byManager.AppointmentId).CreatedByUserId);
    }

    [Fact]
    public async Task Manager_CannotAssignInactiveOrUnknownUser()
    {
        using var f = await Fixture.CreateAsync();
        var passive = await f.Identity.AddUserAsync("pasif", RoleNames.Agent, isActive: false);

        Assert.Equal(OperationResult.Invalid, f.Appointments.TCreate(
            new Appointment { StartAt = DateTime.Today.AddDays(1), AgentUserId = passive.Id }, f.Mert.Id, true));
        Assert.Equal(OperationResult.Invalid, f.Appointments.TCreate(
            new Appointment { StartAt = DateTime.Today.AddDays(1), AgentUserId = 9999 }, f.Mert.Id, true));
    }

    [Fact]
    public async Task Agent_CannotChangeAgentOnUpdate()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Ayse);
        Assert.Equal(OperationResult.Success, f.Appointments.TUpdate(a.AppointmentId,
            new Appointment { StartAt = a.StartAt, AgentUserId = f.Bora.Id, Type = a.Type, Status = a.Status }, f.Ayse.Id, false));
        Assert.Equal(f.Ayse.Id, f.Read(a.AppointmentId).AgentUserId);
    }

    // ---------------- Düzenleme / durum ----------------

    [Fact]
    public async Task UpdateAndStatus_NotVisible_NotFound()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Bora);
        Assert.Equal(OperationResult.NotFound, f.Appointments.TUpdate(a.AppointmentId,
            new Appointment { StartAt = a.StartAt }, f.Ayse.Id, false));
        Assert.Equal(OperationResult.NotFound, f.Appointments.TSetStatus(a.AppointmentId, AppointmentStatus.Cancelled, f.Ayse.Id, false));
        Assert.Equal(AppointmentStatus.Planned, f.Read(a.AppointmentId).Status);
    }

    [Fact]
    public async Task Create_ServiceSetsProtectedFields()
    {
        using var f = await Fixture.CreateAsync();
        var a = new Appointment
        {
            AppointmentId = 555, StartAt = DateTime.Today.AddDays(1), Status = AppointmentStatus.Done,
            CreatedByUserId = f.Bora.Id, CreatedAt = new DateTime(2001, 1, 1), Type = (AppointmentType)99
        };
        Assert.Equal(OperationResult.Success, f.Appointments.TCreate(a, f.Ayse.Id, false));

        var saved = f.Read(a.AppointmentId);
        Assert.NotEqual(555, saved.AppointmentId);
        Assert.Equal(AppointmentStatus.Planned, saved.Status);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
        Assert.True(saved.CreatedAt > DateTime.Now.AddMinutes(-1));
        Assert.Equal(AppointmentType.Meeting, saved.Type);
    }

    [Fact]
    public async Task EndBeforeStart_IsInvalid()
    {
        using var f = await Fixture.CreateAsync();
        var start = DateTime.Today.AddDays(1).AddHours(10);
        Assert.Equal(OperationResult.Invalid, f.Appointments.TCreate(
            new Appointment { StartAt = start, EndAt = start.AddHours(-1) }, f.Ayse.Id, false));
    }

    [Fact]
    public async Task SetStatus_InvalidValue_IsInvalid()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Ayse);
        Assert.Equal(OperationResult.Invalid, f.Appointments.TSetStatus(a.AppointmentId, (AppointmentStatus)99, f.Ayse.Id, false));
    }

    // Sabitleme: bugün "ajandayı gören düzenler" (kullanıcı kararı). Görünürlük ya da düzenleme kuralı
    // değişirse bu test bilerek güncellenmeli (ikisi ayrı kural; biri diğerini sessizce genişletmesin).
    [Fact]
    public async Task EditRule_CurrentlyMatchesVisibility()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse);
        f.Add(f.Bora);
        f.Add(f.Mert, agent: f.Cem.Id);

        foreach (var user in new[] { f.Ayse, f.Bora, f.Cem, f.Mert })
            Assert.All(f.List(user), r => Assert.True(f.Appointments.TCanEdit(r.Appointment, user.Id, f.IsManager(user))));

        var bora = f.List(f.Bora).Single().Appointment;
        Assert.False(f.Appointments.TCanEdit(bora, f.Ayse.Id, false));
    }

    // ---------------- Controller ----------------

    [Fact]
    public async Task Controller_Index_ShowsOnlyVisible()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, notes: "Ayşe");
        f.Add(f.Bora, notes: "Bora");

        var model = ModelOf<AppointmentListViewModel>(f.As(f.Ayse).Index());
        Assert.Equal(new[] { "Ayşe" }, model.Items.Select(i => i.Notes));
        Assert.False(model.CanSeeAll);
        Assert.Equal(2, ModelOf<AppointmentListViewModel>(f.As(f.Mert).Index()).TotalCount);
    }

    [Fact]
    public async Task Controller_EditAndStatus_NotVisible_404()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Bora);

        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Edit(a.AppointmentId));
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Edit(a.AppointmentId, Form()));
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).SetStatus(a.AppointmentId, AppointmentStatus.Cancelled));
        Assert.Equal(AppointmentStatus.Planned, f.Read(a.AppointmentId).Status);
    }

    [Fact]
    public async Task Controller_Edit_NotVisibleChecked_BeforeValidation()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Bora);
        var c = f.As(f.Ayse);
        c.ModelState.AddModelError("StartAt", "zorunlu");
        Assert.IsType<NotFoundResult>(c.Edit(a.AppointmentId, new AppointmentFormViewModel()));
    }

    [Fact]
    public async Task Controller_Create_PrefillsOnlyVisibleRecords()
    {
        using var f = await Fixture.CreateAsync();
        var hiddenContact = f.AddContact(f.Bora, Visibility.OnlyMe);
        var hiddenProperty = f.AddProperty(f.Bora, Visibility.OnlyMe);
        var ownProperty = f.AddProperty(f.Ayse, Visibility.OnlyMe, "Benim Portföyüm");

        var hidden = ModelOf<AppointmentFormViewModel>(f.As(f.Ayse).Create(hiddenContact.ContactId, hiddenProperty.PropertyId, null));
        Assert.Null(hidden.ContactId);
        Assert.Null(hidden.PropertyId);
        Assert.Null(hidden.Location);

        var showing = ModelOf<AppointmentFormViewModel>(f.As(f.Ayse).Create(null, ownProperty.PropertyId, AppointmentType.Showing));
        Assert.Equal(ownProperty.PropertyId, showing.PropertyId);
        Assert.Equal(AppointmentType.Showing, showing.Type);
        Assert.Null(showing.Location); // adres kopyalanmaz (sorumlu değişirse gizli portföyün adresi sızmasın)
    }

    [Fact]
    public async Task Controller_Create_Options_OnlyVisible()
    {
        using var f = await Fixture.CreateAsync();
        f.AddContact(f.Bora, Visibility.OnlyMe, "BoraGizli");
        f.AddContact(f.Bora, Visibility.Public, "BoraAçık");
        f.AddContact(f.Ayse, Visibility.OnlyMe, "AyşeKendi");

        var c = f.As(f.Ayse);
        c.Create(null, null, null);
        var options = Assert.IsType<AppointmentFormOptions>((object)c.ViewBag.Options);
        Assert.Equal(new[] { "AyşeKendi Test", "BoraAçık Test" }, options.Contacts.Select(x => x.Name).OrderBy(x => x));
        Assert.False(options.CanChooseAgent);
        Assert.Empty(options.Agents);
    }

    [Fact]
    public async Task Controller_Create_Valid_SavesForCurrentUser()
    {
        using var f = await Fixture.CreateAsync();
        var result = f.As(f.Ayse).Create(Form(agent: f.Bora.Id));
        Assert.IsType<RedirectToActionResult>(result);

        var saved = Assert.Single(f.List(f.Mert)).Appointment;
        Assert.Equal(f.Ayse.Id, saved.AgentUserId);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
    }

    [Fact]
    public async Task Controller_Create_HiddenContact_ShowsFormError()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe);
        var c = f.As(f.Ayse);

        Assert.IsType<ViewResult>(c.Create(Form(contactId: hidden.ContactId)));
        Assert.False(c.ModelState.IsValid);
        Assert.Empty(f.List(f.Mert));
    }

    [Theory]
    [InlineData(-1)]     // bitiş başlangıçtan önce
    [InlineData(0)]      // bitiş = başlangıç
    public async Task Controller_Create_EndNotAfterStart_Rejected(int hours)
    {
        using var f = await Fixture.CreateAsync();
        var start = DateTime.Today.AddDays(1).AddHours(10);
        var form = Form(start);
        form.EndAt = start.AddHours(hours);
        var c = f.As(f.Ayse);

        Assert.IsType<ViewResult>(c.Create(form));
        Assert.True(c.ModelState.ContainsKey(nameof(AppointmentFormViewModel.EndAt)));
        Assert.Empty(f.List(f.Mert));
    }

    [Fact]
    public async Task Controller_Create_DateOutOfRange_Rejected()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.As(f.Ayse);
        Assert.IsType<ViewResult>(c.Create(Form(new DateTime(1999, 1, 1))));
        Assert.IsType<ViewResult>(c.Create(Form(DateTime.Today.AddYears(3))));
        Assert.Empty(f.List(f.Mert));
    }

    [Fact]
    public async Task Controller_SetStatus_ByAgentOfRecord_Works()
    {
        using var f = await Fixture.CreateAsync();
        var a = f.Add(f.Mert, agent: f.Ayse.Id);

        Assert.IsType<RedirectToActionResult>(f.As(f.Ayse).SetStatus(a.AppointmentId, AppointmentStatus.Done));
        Assert.Equal(AppointmentStatus.Done, f.Read(a.AppointmentId).Status);
    }

    [Fact]
    public async Task Controller_Edit_HiddenLink_IdNotInForm()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Mert, Visibility.OnlyMe);
        var a = f.Add(f.Mert, contactId: hidden.ContactId, agent: f.Ayse.Id);

        var c = f.As(f.Ayse);
        var model = ModelOf<AppointmentFormViewModel>(c.Edit(a.AppointmentId));
        Assert.Null(model.ContactId);
        Assert.True(Assert.IsType<AppointmentFormOptions>((object)c.ViewBag.Options).ContactHidden);
    }

    // ---------------- Ana ekran ----------------

    [Fact]
    public async Task Dashboard_UpcomingCountsOnlyVisible()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse);
        f.Add(f.Ayse, DateTime.Today.AddDays(-3));          // geçmiş: sayılmaz
        f.Add(f.Bora);
        f.Add(f.Mert, agent: f.Ayse.Id);

        DashboardViewModel View(AppUser u) => ModelOf<DashboardViewModel>(
            f.Identity.SignedInAs(TestControllers.Dashboard(f.Identity.Db, f.Properties, f.Contacts), u, f.RoleOf(u)).Index());

        var ayse = View(f.Ayse);
        Assert.Equal(2, ayse.UpcomingCount);
        Assert.Equal(2, ayse.Upcoming.Count);
        Assert.Equal(1, View(f.Bora).UpcomingCount);
        Assert.Equal(3, View(f.Mert).UpcomingCount);
    }
}
