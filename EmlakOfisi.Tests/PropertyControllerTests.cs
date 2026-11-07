using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Görev 12: portföy ekranları (controller seviyesi). Gerçek Identity + InMemory.
// Ayşe, Bora, Cem: danışman. Mert: yönetici.
public class PropertyControllerTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public PropertyManager Properties { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
        public AppUser Cem { get; private init; } = null!;
        public AppUser Mert { get; private init; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var contacts = new ContactManager(new EfContactDal(identity.Db));
            return new Fixture
            {
                Identity = identity,
                Contacts = contacts,
                Properties = new PropertyManager(new EfPropertyDal(identity.Db), contacts),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Cem = await identity.AddUserAsync("cem", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        public PropertyController As(AppUser user, string role) =>
            Identity.SignedInAs(TestControllers.Property(Identity.Db, Properties, Contacts), user, role);

        public Property Add(AppUser creator, string title, Visibility visibility, int agent = 0, int? ownerId = null,
            decimal? lat = null, decimal? lng = null)
        {
            var p = new Property
            {
                Title = title, City = "İstanbul", District = "Kadıköy", Price = 1_000_000,
                Visibility = visibility, AgentUserId = agent, OwnerContactId = ownerId, Latitude = lat, Longitude = lng
            };
            var canSeeAll = creator == Mert;
            Assert.Equal(BusinessLayer.Abstract.OperationResult.Success, Properties.TCreate(p, creator.Id, canSeeAll));
            return p;
        }

        public Contact AddContact(AppUser owner, Visibility visibility, string phone, string name = "Sahip")
        {
            var c = new Contact { Name = name, Surname = "Kişi", Phone = phone, Visibility = visibility, IsSeller = true };
            Contacts.TCreate(c, owner.Id);
            return c;
        }

        public Property? Read(int id) => Properties.TGetVisibleById(id, Mert.Id, true);

        public void Dispose() => Identity.Dispose();
    }

    private static PropertyFormViewModel Form(string title = "Yeni Portföy", string price = "2500000",
        int agent = 0, int? owner = null, string? location = null) => new()
    {
        Title = title, PriceText = price, City = "İstanbul", District = "Üsküdar",
        AgentUserId = agent, OwnerContactId = owner, LocationText = location, Visibility = Visibility.OnlyMe
    };

    private static T ModelOf<T>(IActionResult r) => Assert.IsType<T>(Assert.IsType<ViewResult>(r).Model);

    // ---------------- Liste ----------------

    [Fact]
    public async Task Index_Agent_DoesNotSeeOthersHidden_ButSeesAssigned()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Bora, "Bora Gizli", Visibility.OnlyMe);
        f.Add(f.Mert, "Cem'e Atanan", Visibility.OnlyMe, agent: f.Cem.Id);
        f.Add(f.Bora, "Bora Açık", Visibility.Public);

        var forCem = ModelOf<PropertyListViewModel>(f.As(f.Cem, RoleNames.Agent).Index(null, null, null, null, null, null));
        Assert.Equal(new[] { "Bora Açık", "Cem'e Atanan" }, forCem.Items.Select(i => i.Title).OrderBy(t => t));
        Assert.True(forCem.Items.Single(i => i.Title == "Cem'e Atanan").IsMine);

        var forMert = ModelOf<PropertyListViewModel>(f.As(f.Mert, RoleNames.Manager).Index(null, null, null, null, null, null));
        Assert.Equal(3, forMert.TotalCount);
    }

    [Fact]
    public async Task Index_AgentName_IsOnlyName()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, "Ayşe Portföy", Visibility.Public);

        var item = ModelOf<PropertyListViewModel>(f.As(f.Bora, RoleNames.Agent).Index(null, null, null, null, null, null)).Items.Single();
        Assert.Equal("ayse Test", item.AgentName);
        // ViewModel'de kullanıcı nesnesi yok: sadece metin
        Assert.DoesNotContain(typeof(PropertyListItemViewModel).GetProperties(), p => p.PropertyType == typeof(AppUser));
    }

    // ---------------- Detay ----------------

    [Fact]
    public async Task Detail_Hidden_Returns404()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Add(f.Bora, "Bora Gizli", Visibility.OnlyMe);

        Assert.IsType<NotFoundResult>(f.As(f.Ayse, RoleNames.Agent).Detail(hidden.PropertyId));
    }

    [Fact]
    public async Task Detail_HiddenOwner_ShowsNoNameNoIdNoPhone()
    {
        using var f = await Fixture.CreateAsync();
        var ownerOfAyse = f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 600 00 01", "Gizlisahip");
        var p = f.Add(f.Ayse, "Herkese Açık Daire", Visibility.Public, ownerId: ownerOfAyse.ContactId);

        var forBora = ModelOf<PropertyDetailViewModel>(f.As(f.Bora, RoleNames.Agent).Detail(p.PropertyId));
        Assert.True(forBora.HasOwner);
        Assert.True(forBora.OwnerHidden);
        Assert.Null(forBora.OwnerContactId);
        Assert.Null(forBora.OwnerName);
        Assert.Null(forBora.OwnerPhone);
        Assert.Null(forBora.OwnerTelLink);

        var forAyse = ModelOf<PropertyDetailViewModel>(f.As(f.Ayse, RoleNames.Agent).Detail(p.PropertyId));
        Assert.False(forAyse.OwnerHidden);
        Assert.Equal("Gizlisahip Kişi", forAyse.OwnerName);
        Assert.Equal("tel:+901006000001", forAyse.OwnerTelLink);
    }

    [Fact]
    public async Task Detail_MapLink_FromCoordinates_OrAddress()
    {
        using var f = await Fixture.CreateAsync();
        var withCoords = f.Add(f.Ayse, "Konumlu", Visibility.OnlyMe, lat: 40.984900m, lng: 29.026200m);
        var noCoords = f.Add(f.Ayse, "Konumsuz", Visibility.OnlyMe);
        var c = f.As(f.Ayse, RoleNames.Agent);

        Assert.Equal("https://www.google.com/maps?q=40.984900%2C29.026200", ModelOf<PropertyDetailViewModel>(c.Detail(withCoords.PropertyId)).MapLink);
        Assert.StartsWith("https://www.google.com/maps/search/?api=1&query=", ModelOf<PropertyDetailViewModel>(c.Detail(noCoords.PropertyId)).MapLink);
    }

    [Fact]
    public async Task Detail_EditDeleteButtons_ForAssignedAgent()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Add(f.Mert, "Cem'e Atanan", Visibility.OnlyMe, agent: f.Cem.Id);

        var m = ModelOf<PropertyDetailViewModel>(f.As(f.Cem, RoleNames.Agent).Detail(p.PropertyId));
        Assert.True(m.CanEdit);
        Assert.False(m.CanDelete); // sorumlu silemez
    }

    // ---------------- Ekleme ----------------

    [Fact]
    public async Task Create_Agent_CannotAssignOther_AndOwnerIsSelf()
    {
        using var f = await Fixture.CreateAsync();

        var redirect = Assert.IsType<RedirectToActionResult>(f.As(f.Ayse, RoleNames.Agent).Create(Form(agent: f.Bora.Id, location: "40.98765, 29.03123")));

        var saved = f.Read((int)redirect.RouteValues!["id"]!)!;
        Assert.Equal(f.Ayse.Id, saved.AgentUserId);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
        Assert.Equal(2_500_000m, saved.Price);
        Assert.Equal(40.98765m, saved.Latitude);
        Assert.Equal(29.03123m, saved.Longitude);
        Assert.Equal(Visibility.OnlyMe, saved.Visibility);
    }

    [Fact]
    public async Task Create_Manager_CanAssignAgent()
    {
        using var f = await Fixture.CreateAsync();

        var redirect = Assert.IsType<RedirectToActionResult>(f.As(f.Mert, RoleNames.Manager).Create(Form(agent: f.Cem.Id)));

        Assert.Equal(f.Cem.Id, f.Read((int)redirect.RouteValues!["id"]!)!.AgentUserId);
    }

    [Fact]
    public async Task Edit_Manager_InactiveAgent_StaysSelected_AndIsKeptOnSave()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Add(f.Mert, "Pasif sorumlu", Visibility.OnlyMe, agent: f.Cem.Id);
        f.Cem.IsActive = false;
        await f.Identity.UserManager.UpdateAsync(f.Cem);
        var c = f.As(f.Mert, RoleNames.Manager);

        // Pasif sorumlu listede seçili ilk seçenek olarak durur (tarayıcı başkasını göndermesin)
        var view = Assert.IsType<ViewResult>(c.Edit(p.PropertyId));
        var options = Assert.IsType<PropertyFormOptions>(view.ViewData["Options"]);
        Assert.Equal(f.Cem.Id, options.Agents[0].Id);
        Assert.EndsWith("(pasif)", options.Agents[0].Name);
        Assert.Equal(f.Cem.Id, Assert.IsType<PropertyFormViewModel>(view.Model).AgentUserId);

        // Aynı id geri gelince sorumlu değişmez, kayıt başarılı
        Assert.IsType<RedirectToActionResult>(c.Edit(p.PropertyId, Form("Fiyat düzeltildi", agent: f.Cem.Id)));
        var saved = f.Read(p.PropertyId)!;
        Assert.Equal(f.Cem.Id, saved.AgentUserId);
        Assert.Equal("Fiyat düzeltildi", saved.Title);
    }

    // ---------------- İlan linki (görev 12b) ----------------

    [Fact]
    public async Task ListingUrl_IsSaved_ShownInDetail_AndPrefilledInEdit()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.As(f.Ayse, RoleNames.Agent);
        var form = Form();
        form.ListingUrl = "  https://www.sahibinden.com/ilan/emlak-konut-satilik-deneme-123  ";

        var redirect = Assert.IsType<RedirectToActionResult>(c.Create(form));
        var id = (int)redirect.RouteValues!["id"]!;

        Assert.Equal("https://www.sahibinden.com/ilan/emlak-konut-satilik-deneme-123", f.Read(id)!.ListingUrl);
        Assert.Equal(f.Read(id)!.ListingUrl, ModelOf<PropertyDetailViewModel>(c.Detail(id)).ListingUrl);
        Assert.Equal(f.Read(id)!.ListingUrl, ModelOf<PropertyFormViewModel>(c.Edit(id)).ListingUrl);

        // Boş bırakılırsa link kaldırılır
        var clear = Form();
        clear.ListingUrl = "  ";
        Assert.IsType<RedirectToActionResult>(c.Edit(id, clear));
        Assert.Null(f.Read(id)!.ListingUrl);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("www.sahibinden.com/ilan/1")]   // şema yok
    [InlineData("/Property/Detail/1")]          // göreli adres
    [InlineData("ftp://ornek.test/ilan")]
    public async Task ListingUrl_Unsafe_IsRejected_InControllerAndService(string url)
    {
        using var f = await Fixture.CreateAsync();
        var c = f.As(f.Ayse, RoleNames.Agent);
        var form = Form();
        form.ListingUrl = url;

        Assert.IsType<ViewResult>(c.Create(form));
        Assert.True(c.ModelState.ContainsKey(nameof(PropertyFormViewModel.ListingUrl)));
        Assert.Empty(f.Identity.Db.Properties);

        // Controller atlansa bile servis kabul etmez
        var p = new Property { Title = "X", City = "A", District = "B", ListingUrl = url };
        Assert.Equal(BusinessLayer.Abstract.OperationResult.Invalid, f.Properties.TCreate(p, f.Ayse.Id, false));

        var existing = f.Add(f.Ayse, "Var olan", Visibility.OnlyMe);
        var changes = new Property { Title = "X", City = "A", District = "B", ListingUrl = url };
        Assert.Equal(BusinessLayer.Abstract.OperationResult.Invalid, f.Properties.TUpdate(existing.PropertyId, changes, f.Ayse.Id, false));
        Assert.Null(f.Read(existing.PropertyId)!.ListingUrl);
    }

    [Theory]
    [InlineData("abc", null)]          // fiyat harf
    [InlineData("12,5", null)]         // fiyat virgül
    [InlineData("1000", "40,98 29,03")] // konum biçimi yanlış
    [InlineData("1000", "95, 29")]     // enlem aralık dışı
    public async Task Create_InvalidPriceOrLocation_ReturnsForm(string price, string? location)
    {
        using var f = await Fixture.CreateAsync();
        var c = f.As(f.Ayse, RoleNames.Agent);

        Assert.IsType<ViewResult>(c.Create(Form(price: price, location: location)));
        Assert.False(c.ModelState.IsValid);
        Assert.Empty(f.Identity.Db.Properties);
    }

    [Fact]
    public async Task Create_WithHiddenOwner_ReturnsFormError_AndNothingSaved()
    {
        using var f = await Fixture.CreateAsync();
        var hiddenOfBora = f.AddContact(f.Bora, Visibility.OnlyMe, "0100 600 00 02");
        var c = f.As(f.Ayse, RoleNames.Agent);

        Assert.IsType<ViewResult>(c.Create(Form(owner: hiddenOfBora.ContactId)));
        Assert.False(c.ModelState.IsValid);
        Assert.Empty(f.Identity.Db.Properties);
    }

    [Fact]
    public async Task Create_Get_OwnerOptions_OnlyVisibleSellers()
    {
        using var f = await Fixture.CreateAsync();
        f.AddContact(f.Ayse, Visibility.OnlyMe, "0100 600 00 03", "Ayşeninki");
        f.AddContact(f.Bora, Visibility.OnlyMe, "0100 600 00 04", "Boranınki");
        var c = f.As(f.Ayse, RoleNames.Agent);

        var view = Assert.IsType<ViewResult>(c.Create());
        var options = Assert.IsType<PropertyFormOptions>(view.ViewData["Options"]);

        Assert.Contains(options.Owners, o => o.Name == "Ayşeninki Kişi");
        Assert.DoesNotContain(options.Owners, o => o.Name == "Boranınki Kişi");
        Assert.False(options.CanChooseAgent);
        Assert.Empty(options.Agents);
    }

    // ---------------- Düzenleme ----------------

    [Fact]
    public async Task Edit_OthersPublic_Forbid_Hidden_NotFound()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.Add(f.Bora, "Bora Açık", Visibility.Public);
        var hidden = f.Add(f.Bora, "Bora Gizli", Visibility.OnlyMe);
        var c = f.As(f.Ayse, RoleNames.Agent);

        Assert.IsType<ForbidResult>(c.Edit(open.PropertyId));
        Assert.IsType<NotFoundResult>(c.Edit(hidden.PropertyId));
        Assert.IsType<ForbidResult>(c.Edit(open.PropertyId, Form("Hack")));
        Assert.IsType<NotFoundResult>(c.Edit(hidden.PropertyId, Form(price: "bozuk"))); // form hatasından önce yetki
        Assert.Equal("Bora Açık", f.Read(open.PropertyId)!.Title);
    }

    [Fact]
    public async Task Edit_AssignedAgent_HiddenOwner_NotInForm_AndKeptAfterSave()
    {
        using var f = await Fixture.CreateAsync();
        var ownerOfMert = f.AddContact(f.Mert, Visibility.OnlyMe, "0100 600 00 05");
        var p = f.Add(f.Mert, "Cem'e Atanan", Visibility.OnlyMe, agent: f.Cem.Id, ownerId: ownerOfMert.ContactId);
        var c = f.As(f.Cem, RoleNames.Agent);

        var view = Assert.IsType<ViewResult>(c.Edit(p.PropertyId));
        Assert.Null(Assert.IsType<PropertyFormViewModel>(view.Model).OwnerContactId);   // gizli id forma konmadı
        Assert.True(Assert.IsType<PropertyFormOptions>(view.ViewData["Options"]).OwnerHidden);

        // Cem formu sahip olmadan kaydeder -> sahip korunur
        Assert.IsType<RedirectToActionResult>(c.Edit(p.PropertyId, Form("Cem güncelledi")));
        var saved = f.Read(p.PropertyId)!;
        Assert.Equal("Cem güncelledi", saved.Title);
        Assert.Equal(ownerOfMert.ContactId, saved.OwnerContactId);
        Assert.Equal(f.Cem.Id, saved.AgentUserId);
    }

    [Fact]
    public async Task Edit_Get_PrefillsPriceAndLocation_InvariantFormat()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Add(f.Ayse, "Konumlu", Visibility.OnlyMe, lat: 40.5m, lng: 29.25m);

        var model = ModelOf<PropertyFormViewModel>(f.As(f.Ayse, RoleNames.Agent).Edit(p.PropertyId));
        Assert.Equal("1000000", model.PriceText);
        Assert.Equal("40.5, 29.25", model.LocationText);
    }

    // ---------------- Silme ----------------

    [Fact]
    public async Task Delete_AssignedAgent_Forbid_Creator_Success()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Add(f.Mert, "Cem'e Atanan", Visibility.OnlyMe, agent: f.Cem.Id);
        var own = f.Add(f.Ayse, "Ayşe'nin", Visibility.OnlyMe);

        Assert.IsType<ForbidResult>(f.As(f.Cem, RoleNames.Agent).Delete(p.PropertyId));
        Assert.IsType<NotFoundResult>(f.As(f.Bora, RoleNames.Agent).Delete(own.PropertyId));
        Assert.IsType<RedirectToActionResult>(f.As(f.Ayse, RoleNames.Agent).Delete(own.PropertyId));
        Assert.Null(f.Read(own.PropertyId));
    }

    // ---------------- Biçimler ----------------

    [Theory]
    [InlineData("5000000", true, 5000000)]
    [InlineData("5.000.000", true, 5000000)]
    [InlineData("5 000 000", true, 5000000)]
    [InlineData("12.500", true, 12500)]
    [InlineData("1.5", false, 0)]         // ondalık değil, reddedilir (15 olarak okunmaz)
    [InlineData("8500000.50", false, 0)]
    [InlineData("5.00.000", false, 0)]
    [InlineData("5,5", false, 0)]
    [InlineData("-5", false, 0)]
    [InlineData("", false, 0)]
    public void TryParsePrice(string text, bool ok, decimal expected)
    {
        Assert.Equal(ok, PropertyLabels.TryParsePrice(text, out var price));
        if (ok) Assert.Equal(expected, price);
    }

    [Fact]
    public void PriceText_TurkishFormat()
    {
        Assert.Equal("8.500.000 ₺", PropertyLabels.Price(8_500_000, ListingType.Sale));
        Assert.Equal("32.000 ₺ / ay", PropertyLabels.Price(32_000, ListingType.Rent));
    }

    [Fact]
    public void FormModel_HasNoProtectedFields()
    {
        var props = typeof(PropertyFormViewModel).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("PropertyId", props);
        Assert.DoesNotContain("CreatedByUserId", props);
        Assert.DoesNotContain("IsDeleted", props);
        Assert.DoesNotContain("CreatedAt", props);
    }
}
