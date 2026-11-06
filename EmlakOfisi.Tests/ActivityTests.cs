using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Görev 10: görüşme geçmişi. Kurallar (kullanıcı kararı):
// görme = müşteriyi görebilen; ekleme = müşteriyi düzenleyebilen (sahibi + yönetici); silme = yazan veya yönetici.
// Kullanıcılar: Ayşe ve Bora (danışman), Mert (yönetici).
public class ActivityTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public ActivityManager Activities { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
        public AppUser Mert { get; private init; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var contacts = new ContactManager(new EfContactDal(identity.Db));
            return new Fixture
            {
                Identity = identity,
                Contacts = contacts,
                Activities = new ActivityManager(new EfActivityDal(identity.Db), contacts),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        public Contact AddContact(AppUser owner, Visibility visibility, string phone)
        {
            var c = new Contact { Name = "Musteri", Surname = owner.UserName!, Phone = phone, Visibility = visibility };
            Contacts.TCreate(c, owner.Id);
            return c;
        }

        public Activity Note(string text = "Arandı") =>
            new() { Type = ActivityType.Call, Description = text, ActivityDate = new DateTime(2026, 11, 6, 10, 0, 0) };

        public ActivityController Controller(AppUser user, string role) =>
            Identity.SignedInAs(new ActivityController(Activities), user, role);

        public ContactController ContactController(AppUser user, string role) =>
            Identity.SignedInAs(new ContactController(Contacts, Activities), user, role);

        public void Dispose() => Identity.Dispose();
    }

    // ---------------- Görme ----------------

    [Fact]
    public async Task List_PublicContact_VisibleToOtherAgent()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 01");
        f.Activities.TAdd(c.ContactId, f.Note("Bora aradı"), f.Bora.Id, false);

        var list = f.Activities.TGetForContact(c.ContactId, f.Ayse.Id, false);

        Assert.NotNull(list);
        Assert.Equal("Bora aradı", Assert.Single(list!).Description);
    }

    [Fact]
    public async Task List_HiddenContact_IsNull_ForOtherAgent()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 800 00 02");
        f.Activities.TAdd(c.ContactId, f.Note(), f.Bora.Id, false);

        Assert.Null(f.Activities.TGetForContact(c.ContactId, f.Ayse.Id, false));
        Assert.NotNull(f.Activities.TGetForContact(c.ContactId, f.Mert.Id, true));
    }

    [Fact]
    public async Task List_DeletedContact_IsNull()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 03");
        f.Activities.TAdd(c.ContactId, f.Note(), f.Bora.Id, false);
        f.Contacts.TSoftDelete(c.ContactId, f.Bora.Id, false);

        Assert.Null(f.Activities.TGetForContact(c.ContactId, f.Mert.Id, true));
    }

    [Fact]
    public async Task List_IsNewestFirst_AndIncludesAuthorName()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 04");
        var older = f.Note("Eski"); older.ActivityDate = new DateTime(2026, 1, 1);
        var newer = f.Note("Yeni"); newer.ActivityDate = new DateTime(2026, 6, 1);
        f.Activities.TAdd(c.ContactId, older, f.Ayse.Id, false);
        f.Activities.TAdd(c.ContactId, newer, f.Ayse.Id, false);

        var list = f.Activities.TGetForContact(c.ContactId, f.Ayse.Id, false)!;

        Assert.Equal(new[] { "Yeni", "Eski" }, list.Select(a => a.Description));
        Assert.Equal("ayse", list[0].User!.Name);
    }

    // ---------------- Ekleme ----------------

    [Fact]
    public async Task Add_Owner_Succeeds_AndAuthorIsCurrentUser()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 05");
        var a = f.Note();
        a.UserId = 999;      // sahte değerler: serviste ezilmeli
        a.ContactId = 999;

        Assert.Equal(OperationResult.Success, f.Activities.TAdd(c.ContactId, a, f.Ayse.Id, false));
        Assert.Equal(f.Ayse.Id, a.UserId);
        Assert.Equal(c.ContactId, a.ContactId);
    }

    [Fact]
    public async Task Add_OtherAgentOnPublicContact_IsForbidden()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 06");

        Assert.Equal(OperationResult.Forbidden, f.Activities.TAdd(c.ContactId, f.Note(), f.Ayse.Id, false));
        Assert.Empty(f.Activities.TGetForContact(c.ContactId, f.Bora.Id, false)!);
    }

    [Fact]
    public async Task Add_OtherAgentOnHiddenContact_IsNotFound()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 800 00 07");

        Assert.Equal(OperationResult.NotFound, f.Activities.TAdd(c.ContactId, f.Note(), f.Ayse.Id, false));
    }

    [Fact]
    public async Task Add_Manager_CanAddToAnyContact()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 800 00 08");

        Assert.Equal(OperationResult.Success, f.Activities.TAdd(c.ContactId, f.Note(), f.Mert.Id, true));
    }

    [Fact]
    public async Task Add_InvalidType_FallsBackToNote()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 09");
        var a = f.Note(); a.Type = (ActivityType)99;

        f.Activities.TAdd(c.ContactId, a, f.Ayse.Id, false);

        Assert.Equal(ActivityType.Note, a.Type);
    }

    // ---------------- Silme ----------------

    [Fact]
    public async Task Delete_Author_Succeeds()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 10");
        var a = f.Note();
        f.Activities.TAdd(c.ContactId, a, f.Ayse.Id, false);

        Assert.Equal(OperationResult.Success, f.Activities.TDelete(a.ActivityId, f.Ayse.Id, false, out _));
        Assert.Empty(f.Activities.TGetForContact(c.ContactId, f.Ayse.Id, false)!);
    }

    [Fact]
    public async Task Delete_ContactOwner_CannotDeleteManagersNote()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 11");
        var a = f.Note("Müdür notu");
        f.Activities.TAdd(c.ContactId, a, f.Mert.Id, true);

        // Ayşe müşterinin sahibi ama notu yazan değil
        Assert.Equal(OperationResult.Forbidden, f.Activities.TDelete(a.ActivityId, f.Ayse.Id, false, out _));
    }

    [Fact]
    public async Task Delete_OtherAgent_OnPublicContact_IsForbidden()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 12");
        var a = f.Note();
        f.Activities.TAdd(c.ContactId, a, f.Bora.Id, false);

        Assert.Equal(OperationResult.Forbidden, f.Activities.TDelete(a.ActivityId, f.Ayse.Id, false, out _));
    }

    [Fact]
    public async Task Delete_OnHiddenContact_IsNotFound()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 800 00 13");
        var a = f.Note();
        f.Activities.TAdd(c.ContactId, a, f.Bora.Id, false);

        Assert.Equal(OperationResult.NotFound, f.Activities.TDelete(a.ActivityId, f.Ayse.Id, false, out _));
    }

    [Fact]
    public async Task Delete_Manager_CanDeleteAnyNote()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 800 00 14");
        var a = f.Note();
        f.Activities.TAdd(c.ContactId, a, f.Bora.Id, false);

        Assert.Equal(OperationResult.Success, f.Activities.TDelete(a.ActivityId, f.Mert.Id, true, out _));
    }

    [Fact]
    public async Task Delete_UnknownId_IsNotFound()
    {
        using var f = await Fixture.CreateAsync();
        Assert.Equal(OperationResult.NotFound, f.Activities.TDelete(9999, f.Mert.Id, true, out _));
    }

    // ---------------- Controller ----------------

    [Fact]
    public async Task Controller_Create_OtherAgentOnPublic_Forbid_AndHidden_NotFound()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 15");
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 800 00 16");
        var controller = f.Controller(f.Ayse, RoleNames.Agent);
        var form = new ActivityFormViewModel { Type = ActivityType.Call, Description = "Not" };

        Assert.IsType<ForbidResult>(controller.Create(open.ContactId, form));
        Assert.IsType<NotFoundResult>(controller.Create(hidden.ContactId, form));
    }

    [Fact]
    public async Task Controller_Create_InvalidForm_OnHiddenContact_IsNotFound()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 800 00 17");
        var controller = f.Controller(f.Ayse, RoleNames.Agent);
        controller.ModelState.AddModelError("Description", "zorunlu");

        // Form hatası olsa da önce görünürlük: gizli müşterinin varlığı belli olmasın
        Assert.IsType<NotFoundResult>(controller.Create(hidden.ContactId, new ActivityFormViewModel()));
    }

    [Fact]
    public async Task Controller_Create_Owner_RedirectsToContactDetail()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 18");

        var redirect = Assert.IsType<RedirectToActionResult>(
            f.Controller(f.Ayse, RoleNames.Agent).Create(c.ContactId, new ActivityFormViewModel { Description = "  Arandı  " }));

        Assert.Equal("Contact", redirect.ControllerName);
        Assert.Equal("Arandı", f.Activities.TGetForContact(c.ContactId, f.Ayse.Id, false)!.Single().Description);
    }

    [Fact]
    public async Task ContactDetail_ShowsHistory_ButNoAddForm_ForOtherAgent()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 19");
        f.Activities.TAdd(c.ContactId, f.Note("Bora notu"), f.Bora.Id, false);

        var model = Assert.IsType<ContactDetailViewModel>(
            Assert.IsType<ViewResult>(f.ContactController(f.Ayse, RoleNames.Agent).Detail(c.ContactId)).Model);

        Assert.False(model.CanAddActivity);
        var item = Assert.Single(model.Activities);
        Assert.Equal("Bora notu", item.Description);
        Assert.False(item.CanDelete);
    }

    [Fact]
    public async Task Controller_Create_InvalidForm_OnPublicContactWithoutRight_IsForbidden()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 20");
        var controller = f.Controller(f.Ayse, RoleNames.Agent);
        controller.ModelState.AddModelError("Description", "zorunlu");

        Assert.IsType<ForbidResult>(controller.Create(open.ContactId, new ActivityFormViewModel()));
    }

    [Fact]
    public async Task Controller_Create_InvalidForm_Owner_RedirectsWithError()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 21");
        var controller = f.Controller(f.Ayse, RoleNames.Agent);
        controller.ModelState.AddModelError("Description", "Görüşme notu zorunludur.");

        Assert.IsType<RedirectToActionResult>(controller.Create(c.ContactId, new ActivityFormViewModel()));
        Assert.Equal("Görüşme notu zorunludur.", controller.TempData["Error"]);
        Assert.Empty(f.Activities.TGetForContact(c.ContactId, f.Ayse.Id, false)!);
    }

    [Theory]
    [InlineData(1990)]
    [InlineData(2099)]
    public async Task Controller_Create_DateOutOfRange_IsRejected(int year)
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 22");
        var controller = f.Controller(f.Ayse, RoleNames.Agent);

        controller.Create(c.ContactId, new ActivityFormViewModel { Description = "Not", ActivityDate = new DateTime(year, 1, 1) });

        Assert.NotNull(controller.TempData["Error"]);
        Assert.Empty(f.Activities.TGetForContact(c.ContactId, f.Ayse.Id, false)!);
    }

    [Fact]
    public async Task Controller_Delete_RedirectsToNotsRealContact_AndChecksRealContact()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 800 00 23");
        var a = f.Note();
        f.Activities.TAdd(c.ContactId, a, f.Ayse.Id, false);

        var redirect = Assert.IsType<RedirectToActionResult>(f.Controller(f.Ayse, RoleNames.Agent).Delete(a.ActivityId));

        Assert.Equal(c.ContactId, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Controller_Delete_NotFound_And_Forbid()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 800 00 24");
        var open = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 25");
        var onHidden = f.Note(); f.Activities.TAdd(hidden.ContactId, onHidden, f.Bora.Id, false);
        var onOpen = f.Note(); f.Activities.TAdd(open.ContactId, onOpen, f.Bora.Id, false);
        var controller = f.Controller(f.Ayse, RoleNames.Agent);

        Assert.IsType<NotFoundResult>(controller.Delete(onHidden.ActivityId));
        Assert.IsType<ForbidResult>(controller.Delete(onOpen.ActivityId));
    }

    [Fact]
    public async Task Delete_Manager_CanDeleteOthersNote_OnPublicContact()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 26");
        var a = f.Note(); f.Activities.TAdd(open.ContactId, a, f.Bora.Id, false);

        Assert.Equal(OperationResult.Success, f.Activities.TDelete(a.ActivityId, f.Mert.Id, true, out _));
    }

    [Fact]
    public async Task DeletedContact_AddAndDelete_AreNotFound_EvenForManager()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Bora, Visibility.Public, "0100 800 00 27");
        var a = f.Note(); f.Activities.TAdd(c.ContactId, a, f.Bora.Id, false);
        f.Contacts.TSoftDelete(c.ContactId, f.Bora.Id, false);

        Assert.Equal(OperationResult.NotFound, f.Activities.TAdd(c.ContactId, f.Note(), f.Mert.Id, true));
        Assert.Equal(OperationResult.NotFound, f.Activities.TDelete(a.ActivityId, f.Mert.Id, true, out _));
    }

    [Fact]
    public void ActivityForm_HasNoOwnerFields()
    {
        var props = typeof(ActivityFormViewModel).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("UserId", props);
        Assert.DoesNotContain("ContactId", props);
        Assert.DoesNotContain("ActivityId", props);
    }
}
