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

// Görev 12c: portföyle ilgilenen müşteriler. Gerçek Identity + InMemory.
// Ayşe, Bora, Cem: danışman. Mert: yönetici.
public class PropertyInterestTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public PropertyManager Properties { get; private init; } = null!;
        public PropertyInterestManager Interests { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
        public AppUser Cem { get; private init; } = null!;
        public AppUser Mert { get; private init; } = null!;

        private int _phone;

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
                Interests = TestControllers.Interests(identity.Db, properties, contacts),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Cem = await identity.AddUserAsync("cem", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        public bool SeesAll(AppUser u) => u == Mert;

        public Property Property(AppUser creator, Visibility visibility, string title = "Portföy",
            ListingType listing = ListingType.Sale)
        {
            var p = new Property { Title = title, City = "İstanbul", District = "Kadıköy", Price = 1_000_000,
                                   Visibility = visibility, ListingType = listing };
            Assert.Equal(OperationResult.Success, Properties.TCreate(p, creator.Id, SeesAll(creator)));
            return p;
        }

        public Contact Contact(AppUser owner, Visibility visibility, string name = "Alıcı")
        {
            _phone++;
            var c = new Contact { Name = name, Surname = owner.UserName!, Phone = $"0100 700 00 {_phone:00}",
                                  Visibility = visibility, IsBuyer = true, IsTenant = true };
            Contacts.TCreate(c, owner.Id);
            return c;
        }

        public OperationResult Add(AppUser u, Property p, Contact c, string? note = null) =>
            Interests.TAdd(p.PropertyId, c.ContactId, note, u.Id, SeesAll(u));

        public List<int> ContactIdsOn(Property p, AppUser u) =>
            Interests.TGetForProperty(p.PropertyId, u.Id, SeesAll(u))!.Select(i => i.ContactId).ToList();

        public List<int> PropertyIdsOf(Contact c, AppUser u) =>
            Interests.TGetForContact(c.ContactId, u.Id, SeesAll(u))!.Select(i => i.PropertyId).ToList();

        public PropertyInterestController Controller(AppUser u) =>
            Identity.SignedInAs(new PropertyInterestController(Interests), u, SeesAll(u) ? RoleNames.Manager : RoleNames.Agent);

        public PropertyController PropertyController(AppUser u) =>
            Identity.SignedInAs(TestControllers.Property(Identity.Db, Properties, Contacts), u, SeesAll(u) ? RoleNames.Manager : RoleNames.Agent);

        public ContactController ContactController(AppUser u) =>
            Identity.SignedInAs(TestControllers.Contact(Identity.Db, Contacts), u, SeesAll(u) ? RoleNames.Manager : RoleNames.Agent);

        public void Dispose() => Identity.Dispose();
    }

    private static T ModelOf<T>(IActionResult r) => Assert.IsType<T>(Assert.IsType<ViewResult>(r).Model);

    // ---------------- Ekleme ----------------

    [Fact]
    public async Task Agent_CanAddOwnContact_ToOthersPublicProperty()
    {
        using var f = await Fixture.CreateAsync();
        var boraPublic = f.Property(f.Bora, Visibility.Public);
        var ayseClient = f.Contact(f.Ayse, Visibility.OnlyMe);

        Assert.Equal(OperationResult.Success, f.Add(f.Ayse, boraPublic, ayseClient, "  Hafta sonu bakacak  "));

        var saved = Assert.Single(f.Identity.Db.PropertyInterests);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
        Assert.Equal("Hafta sonu bakacak", saved.Note);
        Assert.Equal(new[] { ayseClient.ContactId }, f.ContactIdsOn(boraPublic, f.Ayse));
        Assert.Equal(new[] { boraPublic.PropertyId }, f.PropertyIdsOf(ayseClient, f.Ayse));
    }

    [Fact]
    public async Task Add_HiddenPropertyOrHiddenContact_IsNotFound_AndNothingSaved()
    {
        using var f = await Fixture.CreateAsync();
        var boraHidden = f.Property(f.Bora, Visibility.OnlyMe);
        var boraHiddenContact = f.Contact(f.Bora, Visibility.OnlyMe);
        var ayseProperty = f.Property(f.Ayse, Visibility.Public);
        var ayseContact = f.Contact(f.Ayse, Visibility.OnlyMe);

        Assert.Equal(OperationResult.NotFound, f.Add(f.Ayse, boraHidden, ayseContact));       // gizli portföy
        Assert.Equal(OperationResult.NotFound, f.Add(f.Ayse, ayseProperty, boraHiddenContact)); // gizli müşteri
        Assert.Empty(f.Identity.Db.PropertyInterests);
    }

    [Fact]
    public async Task Add_Duplicate_OrTooLongNote_IsInvalid()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var c = f.Contact(f.Ayse, Visibility.Public);

        Assert.Equal(OperationResult.Invalid, f.Add(f.Ayse, p, c, new string('x', 501)));
        Assert.Equal(OperationResult.Success, f.Add(f.Ayse, p, c));
        Assert.Equal(OperationResult.Invalid, f.Add(f.Bora, p, c)); // başka biri aynı kişiyi tekrar ekleyemez
        Assert.Single(f.Identity.Db.PropertyInterests);
    }

    [Fact]
    public async Task Add_DeletedContactOrProperty_IsNotFound()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var c = f.Contact(f.Ayse, Visibility.Public);
        var p2 = f.Property(f.Ayse, Visibility.Public);

        Assert.Equal(OperationResult.Success, f.Contacts.TSoftDelete(c.ContactId, f.Ayse.Id, false));
        Assert.Equal(OperationResult.NotFound, f.Add(f.Ayse, p, c));

        var c2 = f.Contact(f.Ayse, Visibility.Public);
        Assert.Equal(OperationResult.Success, f.Properties.TSoftDelete(p2.PropertyId, f.Ayse.Id, false));
        Assert.Equal(OperationResult.NotFound, f.Add(f.Ayse, p2, c2));
    }

    // ---------------- Görme (sızıntı) ----------------

    [Fact]
    public async Task PropertyList_HidesContactsTheViewerCannotSee_WithoutCount()
    {
        using var f = await Fixture.CreateAsync();
        var ayseProperty = f.Property(f.Ayse, Visibility.Public);
        var boraSecret = f.Contact(f.Bora, Visibility.OnlyMe, "Gizli");
        var cemPublic = f.Contact(f.Cem, Visibility.Public, "Açık");
        f.Add(f.Bora, ayseProperty, boraSecret);
        f.Add(f.Cem, ayseProperty, cemPublic);

        // Ayşe portföyün sahibi ama Bora'nın gizli müşterisini görmez
        Assert.Equal(new[] { cemPublic.ContactId }, f.ContactIdsOn(ayseProperty, f.Ayse));
        Assert.Equal(2, f.ContactIdsOn(ayseProperty, f.Bora).Count);
        Assert.Equal(2, f.ContactIdsOn(ayseProperty, f.Mert).Count);

        // Ekranda da sadece görünen satır var; sayı rozeti de 1
        var detail = ModelOf<PropertyDetailViewModel>(f.PropertyController(f.Ayse).Detail(ayseProperty.PropertyId));
        var item = Assert.Single(detail.Interests.Items);
        Assert.Equal(cemPublic.ContactId, item.ContactId);
        Assert.DoesNotContain(detail.Interests.Items, i => i.ContactName.Contains("Gizli"));
    }

    [Fact]
    public async Task ContactList_HidesPropertiesTheViewerCannotSee()
    {
        using var f = await Fixture.CreateAsync();
        var ayseContact = f.Contact(f.Ayse, Visibility.Public);
        var boraSecretProperty = f.Property(f.Bora, Visibility.OnlyMe);
        var cemPublicProperty = f.Property(f.Cem, Visibility.Public);
        f.Add(f.Bora, boraSecretProperty, ayseContact);
        f.Add(f.Cem, cemPublicProperty, ayseContact);

        Assert.Equal(new[] { cemPublicProperty.PropertyId }, f.PropertyIdsOf(ayseContact, f.Ayse));
        Assert.Equal(2, f.PropertyIdsOf(ayseContact, f.Mert).Count);

        var detail = ModelOf<ContactDetailViewModel>(f.ContactController(f.Ayse).Detail(ayseContact.ContactId));
        Assert.Equal(cemPublicProperty.PropertyId, Assert.Single(detail.Interests.Items).PropertyId);
    }

    [Fact]
    public async Task Lists_ForHiddenAnchor_AreNull()
    {
        using var f = await Fixture.CreateAsync();
        var hiddenProperty = f.Property(f.Bora, Visibility.OnlyMe);
        var hiddenContact = f.Contact(f.Bora, Visibility.OnlyMe);

        Assert.Null(f.Interests.TGetForProperty(hiddenProperty.PropertyId, f.Ayse.Id, false));
        Assert.Null(f.Interests.TGetForContact(hiddenContact.ContactId, f.Ayse.Id, false));
    }

    [Fact]
    public async Task DeletedContact_DisappearsFromPropertyList()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var c = f.Contact(f.Ayse, Visibility.Public);
        f.Add(f.Ayse, p, c);

        f.Contacts.TSoftDelete(c.ContactId, f.Ayse.Id, false);

        Assert.Empty(f.ContactIdsOn(p, f.Mert));
    }

    // ---------------- Ekleme seçenekleri ----------------

    [Fact]
    public async Task PropertyDetail_Choices_OnlyVisibleBuyers_NotAlreadyListed()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var mine = f.Contact(f.Ayse, Visibility.OnlyMe, "Benim");
        var listed = f.Contact(f.Ayse, Visibility.OnlyMe, "Ekli");
        var boraSecret = f.Contact(f.Bora, Visibility.OnlyMe, "BoraGizli");
        var boraOpen = f.Contact(f.Bora, Visibility.Public, "BoraAçık");
        f.Add(f.Ayse, p, listed);

        var choices = ModelOf<PropertyDetailViewModel>(f.PropertyController(f.Ayse).Detail(p.PropertyId))
            .Interests.Choices.Select(c => c.Id).ToList();

        Assert.Contains(mine.ContactId, choices);
        Assert.Contains(boraOpen.ContactId, choices);
        Assert.DoesNotContain(boraSecret.ContactId, choices);
        Assert.DoesNotContain(listed.ContactId, choices);
    }

    [Fact]
    public async Task ContactDetail_Choices_OnlyVisibleActiveProperties()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.Contact(f.Ayse, Visibility.OnlyMe);
        var open = f.Property(f.Bora, Visibility.Public, "Açık");
        var secret = f.Property(f.Bora, Visibility.OnlyMe, "Gizli");
        var sold = new Property { Title = "Satıldı", City = "A", District = "B", Visibility = Visibility.Public, Status = PropertyStatus.Sold };
        f.Properties.TCreate(sold, f.Bora.Id, false);

        var choices = ModelOf<ContactDetailViewModel>(f.ContactController(f.Ayse).Detail(c.ContactId))
            .Interests.Choices.Select(x => x.Id).ToList();

        Assert.Equal(new[] { open.PropertyId }, choices);
        Assert.DoesNotContain(secret.PropertyId, choices);
    }

    // ---------------- Silme ----------------

    [Fact]
    public async Task Delete_Matrix_AdderEditorManager_Allowed_OtherAgentForbidden()
    {
        using var f = await Fixture.CreateAsync();
        var ayseProperty = f.Property(f.Ayse, Visibility.Public);

        PropertyInterest AddBy(AppUser u)
        {
            var c = f.Contact(u, Visibility.Public);
            Assert.Equal(OperationResult.Success, f.Add(u, ayseProperty, c));
            return f.Identity.Db.PropertyInterests.Single(i => i.ContactId == c.ContactId);
        }

        // Bora ekledi: Cem (ikisini de görüyor ama ne ekleyen ne düzenleyen) silemez
        var byBora = AddBy(f.Bora);
        Assert.Equal(OperationResult.Forbidden, f.Interests.TDelete(byBora.PropertyInterestId, f.Cem.Id, false, out _, out _));

        // Portföyü düzenleyebilen (oluşturan Ayşe) silebilir
        Assert.Equal(OperationResult.Success, f.Interests.TDelete(byBora.PropertyInterestId, f.Ayse.Id, false, out var pid, out _));
        Assert.Equal(ayseProperty.PropertyId, pid);

        // Ekleyen kendisi silebilir
        var byCem = AddBy(f.Cem);
        Assert.Equal(OperationResult.Success, f.Interests.TDelete(byCem.PropertyInterestId, f.Cem.Id, false, out _, out _));

        // Yönetici silebilir
        var byBora2 = AddBy(f.Bora);
        Assert.Equal(OperationResult.Success, f.Interests.TDelete(byBora2.PropertyInterestId, f.Mert.Id, true, out _, out _));
    }

    [Fact]
    public async Task Delete_WhenContactHidden_IsNotFound_EvenForPropertyCreator()
    {
        using var f = await Fixture.CreateAsync();
        var ayseProperty = f.Property(f.Ayse, Visibility.Public);
        var boraSecret = f.Contact(f.Bora, Visibility.OnlyMe);
        f.Add(f.Bora, ayseProperty, boraSecret);
        var row = f.Identity.Db.PropertyInterests.Single();

        // Ayşe portföyü düzenleyebilir ama satırın müşterisini göremez: varlığı da belli olmaz
        Assert.Equal(OperationResult.NotFound, f.Interests.TDelete(row.PropertyInterestId, f.Ayse.Id, false, out _, out _));
        Assert.Equal(OperationResult.NotFound, f.Interests.TDelete(9999, f.Mert.Id, true, out _, out _));
        Assert.Single(f.Identity.Db.PropertyInterests);
    }

    // ---------------- Controller ----------------

    [Fact]
    public async Task Controller_Add_HiddenProperty_Is404_EmptySelection_RedirectsWithError()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Property(f.Bora, Visibility.OnlyMe);
        var mine = f.Contact(f.Ayse, Visibility.OnlyMe);
        var open = f.Property(f.Ayse, Visibility.Public);
        var c = f.Controller(f.Ayse);

        Assert.IsType<NotFoundResult>(c.Add(hidden.PropertyId, mine.ContactId, null, null));

        var redirect = Assert.IsType<RedirectToActionResult>(c.Add(open.PropertyId, 0, null, null));
        Assert.Equal("Property", redirect.ControllerName);
        Assert.Equal(open.PropertyId, redirect.RouteValues!["id"]);
        Assert.NotNull(c.TempData["Error"]);
        Assert.Empty(f.Identity.Db.PropertyInterests);
    }

    [Fact]
    public async Task Controller_AddFromContactPage_ReturnsToContact_DuplicateShowsError()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var contact = f.Contact(f.Ayse, Visibility.OnlyMe);
        var c = f.Controller(f.Ayse);

        var redirect = Assert.IsType<RedirectToActionResult>(c.Add(p.PropertyId, contact.ContactId, "not", PropertyInterestController.ReturnToContact));
        Assert.Equal("Contact", redirect.ControllerName);
        Assert.Equal(contact.ContactId, redirect.RouteValues!["id"]);

        c.Add(p.PropertyId, contact.ContactId, null, PropertyInterestController.ReturnToContact);
        Assert.Contains("zaten", (string)c.TempData["Error"]!);
        Assert.Single(f.Identity.Db.PropertyInterests);
    }

    [Fact]
    public async Task Controller_Delete_RedirectsToRealProperty_AndMapsForbidden()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var boraContact = f.Contact(f.Bora, Visibility.Public);
        f.Add(f.Bora, p, boraContact);
        var row = f.Identity.Db.PropertyInterests.Single();

        Assert.IsType<ForbidResult>(f.Controller(f.Cem).Delete(row.PropertyInterestId, null));

        var redirect = Assert.IsType<RedirectToActionResult>(f.Controller(f.Bora).Delete(row.PropertyInterestId, null));
        Assert.Equal(p.PropertyId, redirect.RouteValues!["id"]);
        Assert.Empty(f.Identity.Db.PropertyInterests);
    }

    [Fact]
    public async Task InterestItem_ExposesOnlyNames_NotEntities()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var contact = f.Contact(f.Ayse, Visibility.Public);
        f.Add(f.Ayse, p, contact);

        var item = Assert.Single(ModelOf<PropertyDetailViewModel>(f.PropertyController(f.Ayse).Detail(p.PropertyId)).Interests.Items);
        Assert.Equal($"{f.Ayse.Name} {f.Ayse.Surname}", item.AddedBy);
        Assert.StartsWith("tel:+90100", item.TelLink);
        Assert.True(item.CanDelete);

        // ViewModel'de entity tipinde alan yok
        Assert.DoesNotContain(typeof(InterestItemViewModel).GetProperties(),
            pr => pr.PropertyType.Namespace == typeof(Contact).Namespace);
    }
}
