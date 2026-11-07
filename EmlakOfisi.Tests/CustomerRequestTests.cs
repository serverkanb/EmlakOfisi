using System.Reflection;
using System.Security.Claims;
using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Görev 17: müşteri talepleri ve kişi bazlı izin. Ayşe, Bora, Cem: danışman. Mert: yönetici. Ada: admin.
// Görme: müşteriyi gören. Ekleme/düzenleme: müşteriyi düzenleyen ya da "request.edit-any" izni olan.
public class CustomerRequestTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public CustomerRequestManager Requests { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
        public AppUser Cem { get; private init; } = null!;
        public AppUser Mert { get; private init; } = null!;
        public AppUser Ada { get; private init; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var contacts = new ContactManager(new EfContactDal(identity.Db));
            return new Fixture
            {
                Identity = identity,
                Contacts = contacts,
                Requests = TestControllers.Requests(identity.Db, contacts),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Cem = await identity.AddUserAsync("cem", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager),
                Ada = await identity.AddUserAsync("ada", RoleNames.Admin)
            };
        }

        public bool IsManager(AppUser u) => u == Mert || u == Ada;
        public string RoleOf(AppUser u) => u == Mert ? RoleNames.Manager : u == Ada ? RoleNames.Admin : RoleNames.Agent;

        public CustomerRequestController As(AppUser user, bool editAny = false)
        {
            var c = Identity.SignedInAs(TestControllers.Request(Identity.Db, Contacts), user, RoleOf(user));
            if (editAny)
                ((ClaimsIdentity)c.User.Identity!).AddClaim(new Claim(Permissions.ClaimType, Permissions.RequestEditAny));
            return c;
        }

        private int _phone;
        public Contact AddContact(AppUser owner, Visibility visibility, string name = "Kişi")
        {
            var c = new Contact { Name = name, Surname = "Test", Phone = $"0100{++_phone:D7}", Visibility = visibility, IsBuyer = true };
            Contacts.TCreate(c, owner.Id);
            return c;
        }

        public CustomerRequest Add(AppUser user, Contact contact, string? city = "İstanbul", string? district = "Kadıköy",
            ListingType listing = ListingType.Sale, decimal? min = null, decimal? max = null, string? notes = null)
        {
            var r = new CustomerRequest { City = city, District = district, ListingType = listing, MinPrice = min, MaxPrice = max, Notes = notes };
            Assert.Equal(OperationResult.Success, Requests.TCreate(contact.ContactId, r, user.Id, IsManager(user), false));
            return r;
        }

        public List<CustomerRequestRow> List(AppUser user, RequestStatus? status = null, string? q = null) =>
            Requests.TGetVisibleList(user.Id, IsManager(user), new CustomerRequestFilter { Status = status, Search = q }, 1, 100).Items;

        public CustomerRequest Read(int id) => Requests.TGetVisibleById(id, Mert.Id, true)!;

        public void Dispose() => Identity.Dispose();
    }

    private static CustomerRequestFormViewModel Form(string? min = null, string? max = null, string? notes = "Not") => new()
    {
        ListingType = ListingType.Sale, City = "İstanbul", District = "Üsküdar", RoomCount = "3+1",
        MinPriceText = min, MaxPriceText = max, Notes = notes
    };

    private static T ModelOf<T>(IActionResult r) => Assert.IsType<T>(Assert.IsType<ViewResult>(r).Model);

    // ---------------- Görme ----------------

    [Fact]
    public async Task Visibility_FollowsContact()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe, "BoraGizli");
        var open = f.AddContact(f.Bora, Visibility.Public, "BoraAçık");
        var own = f.AddContact(f.Ayse, Visibility.OnlyMe, "AyşeKendi");
        f.Add(f.Bora, hidden, notes: "gizli");
        f.Add(f.Bora, open, notes: "açık");
        f.Add(f.Ayse, own, notes: "kendi");

        Assert.Equal(new[] { "açık", "kendi" }, f.List(f.Ayse).Select(r => r.Request.Notes).OrderBy(n => n));
        Assert.Equal(new[] { "açık" }, f.List(f.Cem).Select(r => r.Request.Notes));
        Assert.Equal(3, f.List(f.Mert).Count);
    }

    [Fact]
    public async Task Search_DoesNotLeakHiddenContactsRequests()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe);
        f.Add(f.Bora, hidden, city: "İzmir", district: "Urla");

        Assert.Empty(f.List(f.Ayse, q: "Urla"));
        Assert.Single(f.List(f.Bora, q: "Urla"));
        Assert.Null(f.Requests.TGetVisibleById(f.List(f.Bora).Single().Request.CustomerRequestId, f.Ayse.Id, false));
    }

    [Fact]
    public async Task DeletedContact_HidesRequests()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.Public);
        f.Add(f.Ayse, c);
        f.Contacts.TSoftDelete(c.ContactId, f.Ayse.Id, false);

        Assert.Empty(f.List(f.Mert));
    }

    [Fact]
    public async Task Status_Filter_AndOpenFirst()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var closed = f.Add(f.Ayse, c, notes: "kapalı");
        f.Add(f.Ayse, c, notes: "açık");
        Assert.Equal(OperationResult.Success, f.Requests.TSetStatus(closed.CustomerRequestId, RequestStatus.Closed, f.Ayse.Id, false, false, out var cid));
        Assert.Equal(c.ContactId, cid);

        Assert.Equal(new[] { "açık" }, f.List(f.Ayse, RequestStatus.Open).Select(r => r.Request.Notes));
        Assert.Equal(new[] { "kapalı" }, f.List(f.Ayse, RequestStatus.Closed).Select(r => r.Request.Notes));
        Assert.Equal(new[] { "açık", "kapalı" }, f.List(f.Ayse).Select(r => r.Request.Notes));
    }

    // ---------------- Ekleme / düzenleme yetkisi ----------------

    [Fact]
    public async Task Add_OwnerAndManager_Allowed_OtherAgent_Forbidden_HiddenNotFound()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.AddContact(f.Bora, Visibility.Public);
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe);

        Assert.Equal(OperationResult.Success, f.Requests.TCheckAdd(open.ContactId, f.Bora.Id, false, false));
        Assert.Equal(OperationResult.Success, f.Requests.TCheckAdd(open.ContactId, f.Mert.Id, true, false));
        Assert.Equal(OperationResult.Forbidden, f.Requests.TCheckAdd(open.ContactId, f.Ayse.Id, false, false));
        Assert.Equal(OperationResult.NotFound, f.Requests.TCheckAdd(hidden.ContactId, f.Ayse.Id, false, false));
    }

    [Fact]
    public async Task Permission_AllowsOtherAgentsPublicContact_ButNotHidden()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.AddContact(f.Bora, Visibility.Public);
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe);

        Assert.Equal(OperationResult.Success, f.Requests.TCreate(open.ContactId,
            new CustomerRequest { City = "Ankara" }, f.Ayse.Id, false, canEditOthers: true));
        // İzin görünürlüğü değiştirmez
        Assert.Equal(OperationResult.NotFound, f.Requests.TCreate(hidden.ContactId,
            new CustomerRequest { City = "Ankara" }, f.Ayse.Id, false, canEditOthers: true));

        var r = f.List(f.Bora).Single().Request;
        Assert.Equal(OperationResult.Forbidden, f.Requests.TUpdate(r.CustomerRequestId, new CustomerRequest { City = "X" }, f.Cem.Id, false, false));
        Assert.Equal(OperationResult.Success, f.Requests.TUpdate(r.CustomerRequestId, new CustomerRequest { City = "Y" }, f.Cem.Id, false, true));
        Assert.Equal("Y", f.Read(r.CustomerRequestId).City);
    }

    [Fact]
    public async Task UpdateAndStatus_HiddenContact_NotFound()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe);
        var r = f.Add(f.Bora, hidden);

        Assert.Equal(OperationResult.NotFound, f.Requests.TUpdate(r.CustomerRequestId, new CustomerRequest(), f.Ayse.Id, false, true));
        Assert.Equal(OperationResult.NotFound, f.Requests.TSetStatus(r.CustomerRequestId, RequestStatus.Closed, f.Ayse.Id, false, true, out _));
        Assert.Equal(RequestStatus.Open, f.Read(r.CustomerRequestId).Status);
    }

    [Fact]
    public async Task Create_ServiceSetsProtectedFields()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var other = f.AddContact(f.Bora, Visibility.OnlyMe);
        var r = new CustomerRequest
        {
            CustomerRequestId = 777, ContactId = other.ContactId, CreatedByUserId = f.Bora.Id,
            CreatedAt = new DateTime(2001, 1, 1), Status = RequestStatus.Closed, ListingType = (ListingType)99
        };
        Assert.Equal(OperationResult.Success, f.Requests.TCreate(c.ContactId, r, f.Ayse.Id, false, false));

        var saved = f.Read(r.CustomerRequestId);
        Assert.NotEqual(777, saved.CustomerRequestId);
        Assert.Equal(c.ContactId, saved.ContactId);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
        Assert.Equal(RequestStatus.Open, saved.Status);
        Assert.Equal(ListingType.Sale, saved.ListingType);
        Assert.True(saved.CreatedAt > DateTime.Now.AddMinutes(-1));
    }

    [Fact]
    public async Task Update_DoesNotMoveRequestToAnotherContact()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var other = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var r = f.Add(f.Ayse, c);

        Assert.Equal(OperationResult.Success, f.Requests.TUpdate(r.CustomerRequestId,
            new CustomerRequest { ContactId = other.ContactId, CreatedByUserId = f.Bora.Id, Status = RequestStatus.Closed, City = "Bursa" },
            f.Ayse.Id, false, false));
        var saved = f.Read(r.CustomerRequestId);
        Assert.Equal(c.ContactId, saved.ContactId);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
        Assert.Equal(RequestStatus.Open, saved.Status);
        Assert.Equal("Bursa", saved.City);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -5)]
    [InlineData(10, 5)]
    public async Task InvalidPrices_AreInvalid(int? min, int? max)
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        Assert.Equal(OperationResult.Invalid, f.Requests.TCreate(c.ContactId,
            new CustomerRequest { MinPrice = min, MaxPrice = max }, f.Ayse.Id, false, false));
    }

    // ---------------- Controller ----------------

    [Fact]
    public void Controller_RequiresStaff()
    {
        Assert.Equal(RoleNames.Staff, typeof(CustomerRequestController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public async Task Controller_Create_HiddenContact404_OtherAgent403_BeforeValidation()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe);
        var open = f.AddContact(f.Bora, Visibility.Public);

        var c1 = f.As(f.Ayse);
        c1.ModelState.AddModelError("x", "geçersiz");
        Assert.IsType<NotFoundResult>(c1.Create(hidden.ContactId, new CustomerRequestFormViewModel()));
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Create(hidden.ContactId));

        var c2 = f.As(f.Ayse);
        c2.ModelState.AddModelError("x", "geçersiz");
        Assert.IsType<ForbidResult>(c2.Create(open.ContactId, new CustomerRequestFormViewModel()));
        Assert.IsType<ForbidResult>(f.As(f.Ayse).Create(open.ContactId));
        Assert.Empty(f.List(f.Mert));
    }

    [Fact]
    public async Task Controller_Permission_AllowsCreateOnOthersPublicContact()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.AddContact(f.Bora, Visibility.Public);

        Assert.IsType<RedirectToActionResult>(f.As(f.Ayse, editAny: true).Create(open.ContactId, Form("1.000.000", "2000000")));
        var saved = f.List(f.Mert).Single().Request;
        Assert.Equal(1_000_000, saved.MinPrice);
        Assert.Equal(2_000_000, saved.MaxPrice);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
    }

    [Theory]
    [InlineData("abc", null)]
    [InlineData("1.5", null)]
    [InlineData("5000000", "1000000")]   // en çok < en az
    public async Task Controller_Create_InvalidPrice_ShowsForm(string? min, string? max)
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var controller = f.As(f.Ayse);

        Assert.IsType<ViewResult>(controller.Create(c.ContactId, Form(min, max)));
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(f.List(f.Mert));
    }

    [Fact]
    public async Task Controller_EditAndStatus_OtherAgent403_Hidden404()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.AddContact(f.Bora, Visibility.Public);
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe);
        var r1 = f.Add(f.Bora, open);
        var r2 = f.Add(f.Bora, hidden);

        Assert.IsType<ForbidResult>(f.As(f.Ayse).Edit(r1.CustomerRequestId));
        Assert.IsType<ForbidResult>(f.As(f.Ayse).Edit(r1.CustomerRequestId, Form()));
        Assert.IsType<ForbidResult>(f.As(f.Ayse).SetStatus(r1.CustomerRequestId, RequestStatus.Closed));
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Edit(r2.CustomerRequestId));
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).SetStatus(r2.CustomerRequestId, RequestStatus.Closed));
        Assert.Equal(RequestStatus.Open, f.Read(r1.CustomerRequestId).Status);

        // izinli danışman kapatabilir, ama gizli olanı yine bulamaz
        Assert.IsType<RedirectToActionResult>(f.As(f.Ayse, editAny: true).SetStatus(r1.CustomerRequestId, RequestStatus.Closed));
        Assert.IsType<NotFoundResult>(f.As(f.Ayse, editAny: true).SetStatus(r2.CustomerRequestId, RequestStatus.Closed));
        Assert.Equal(RequestStatus.Closed, f.Read(r1.CustomerRequestId).Status);
    }

    [Fact]
    public async Task Controller_Index_DefaultOpen_AllAndClosed()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var closed = f.Add(f.Ayse, c, notes: "kapalı");
        f.Add(f.Ayse, c, notes: "açık");
        f.Requests.TSetStatus(closed.CustomerRequestId, RequestStatus.Closed, f.Ayse.Id, false, false, out _);

        Assert.Equal(new[] { "açık" }, ModelOf<CustomerRequestListViewModel>(f.As(f.Ayse).Index(null, null, null)).Items.Select(i => i.Notes));
        Assert.Equal(2, ModelOf<CustomerRequestListViewModel>(f.As(f.Ayse).Index(null, null, null, "all")).TotalCount);
        Assert.Equal(new[] { "kapalı" }, ModelOf<CustomerRequestListViewModel>(f.As(f.Ayse).Index(null, null, null, "Closed")).Items.Select(i => i.Notes));
        Assert.Empty(ModelOf<CustomerRequestListViewModel>(f.As(f.Bora).Index(null, null, null, "all")).Items);
    }

    [Fact]
    public void MatchRoute_UsesRequestCriteria()
    {
        var item = new CustomerRequestItemViewModel
        {
            ListingType = ListingType.Rent, PropertyType = PropertyType.Residence, City = "İstanbul", District = "Kadıköy",
            MinPrice = 20000, MaxPrice = 35000
        };
        var route = item.MatchRoute;
        Assert.Equal("Rent", route["listingType"]);
        Assert.Equal("Residence", route["propertyType"]);
        Assert.Equal("Active", route["status"]);
        Assert.Equal("20000", route["minPrice"]);
        Assert.Equal("35000", route["maxPrice"]);
        Assert.Equal("Kadıköy", route["q"]);
        Assert.Equal("Kiralık · Konut · Kadıköy / İstanbul", item.Summary);
        Assert.Equal("20.000 – 35.000 ₺ / ay", item.PriceText);
    }

    // ---------------- Kişi bazlı izin sayfası ----------------

    [Fact]
    public void UserPermissionController_IsAdminOnly()
    {
        Assert.Equal(RoleNames.Admin, typeof(UserPermissionController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public async Task UserPermission_GrantAndRevoke_OnlyKnownPermissions()
    {
        using var f = await Fixture.CreateAsync();
        UserPermissionController Admin() =>
            f.Identity.SignedInAs(new UserPermissionController(f.Identity.UserManager, f.Identity.SignInManager), f.Ada, RoleNames.Admin);
        var stampBefore = await f.Identity.UserManager.GetSecurityStampAsync(f.Ayse);

        Assert.IsType<RedirectToActionResult>(await Admin().Edit(f.Ayse.Id, new List<string> { Permissions.RequestEditAny, "bilinmeyen.izin" }));
        var claims = await f.Identity.UserManager.GetClaimsAsync(f.Ayse);
        Assert.Equal(new[] { Permissions.RequestEditAny }, claims.Where(c => c.Type == Permissions.ClaimType).Select(c => c.Value));
        Assert.NotEqual(stampBefore, await f.Identity.UserManager.GetSecurityStampAsync(f.Ayse));   // oturum yenilenir

        var page = ModelOf<UserPermissionPageViewModel>(await Admin().Edit(f.Ayse.Id));
        Assert.True(page.Permissions.Single(p => p.Name == Permissions.RequestEditAny).Granted);

        // Tekrar kaydetmek çift kayıt oluşturmaz; boş gönderim izni kaldırır
        await Admin().Edit(f.Ayse.Id, new List<string> { Permissions.RequestEditAny });
        Assert.Single(await f.Identity.UserManager.GetClaimsAsync(f.Ayse));
        await Admin().Edit(f.Ayse.Id, null);
        Assert.Empty(await f.Identity.UserManager.GetClaimsAsync(f.Ayse));

        Assert.IsType<NotFoundResult>(await Admin().Edit(9999));
    }
}
