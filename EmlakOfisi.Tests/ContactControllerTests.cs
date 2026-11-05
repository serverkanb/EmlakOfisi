using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// İlk ekranın kabul kriterleri (docs/MIMARI_PLAN.md bölüm 7) controller seviyesinde.
public class ContactControllerTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Service { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
        public AppUser Mert { get; private init; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var f = new Fixture
            {
                Identity = identity,
                Service = new ContactManager(new EfContactDal(identity.Db)),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
            return f;
        }

        public Contact Add(AppUser owner, string name, Visibility visibility, string phone = "0555 000 00 00")
        {
            var c = new Contact { Name = name, Surname = "Test", Phone = phone, Visibility = visibility, IsBuyer = true };
            Service.TCreate(c, owner.Id);
            return c;
        }

        public ContactController Controller(AppUser user, string role) =>
            Identity.SignedInAs(new ContactController(Service), user, role);

        public void Dispose() => Identity.Dispose();
    }

    private static List<string> Names(IActionResult result) =>
        Assert.IsType<ContactListViewModel>(Assert.IsType<ViewResult>(result).Model)
            .Items.Select(i => i.FullName).OrderBy(n => n).ToList();

    [Fact]
    public async Task Index_Agent_SeesOwnAndPublic_NotOthersOnlyMe()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, "AyseGizli", Visibility.OnlyMe);
        f.Add(f.Bora, "BoraGizli", Visibility.OnlyMe);
        f.Add(f.Bora, "BoraAcik", Visibility.Public);

        var names = Names(f.Controller(f.Ayse, RoleNames.Agent).Index(null, null));

        Assert.Equal(new[] { "AyseGizli Test", "BoraAcik Test" }, names);
    }

    [Fact]
    public async Task Index_Manager_SeesEverything()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, "AyseGizli", Visibility.OnlyMe);
        f.Add(f.Bora, "BoraGizli", Visibility.OnlyMe);

        Assert.Equal(2, Names(f.Controller(f.Mert, RoleNames.Manager).Index(null, null)).Count);
    }

    [Fact]
    public async Task Index_SearchForOthersOnlyMe_ReturnsNothing()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Bora, "BoraGizli", Visibility.OnlyMe, "0555 123 45 67");

        var controller = f.Controller(f.Ayse, RoleNames.Agent);

        Assert.Empty(Names(controller.Index("BoraGizli", null)));
        Assert.Empty(Names(controller.Index("123 45 67", null)));
    }

    [Fact]
    public async Task Detail_OthersOnlyMe_Returns404()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Add(f.Bora, "BoraGizli", Visibility.OnlyMe);

        Assert.IsType<NotFoundResult>(f.Controller(f.Ayse, RoleNames.Agent).Detail(hidden.ContactId));
    }

    [Fact]
    public async Task Detail_OthersPublic_IsVisible_ButNotEditable()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.Add(f.Bora, "BoraAcik", Visibility.Public);

        var view = Assert.IsType<ViewResult>(f.Controller(f.Ayse, RoleNames.Agent).Detail(open.ContactId));
        var model = Assert.IsType<ContactDetailViewModel>(view.Model);

        Assert.False(model.CanEdit);
        Assert.False(model.CanDelete);
        Assert.False(model.Summary.IsMine);
    }

    [Fact]
    public async Task Detail_Manager_SeesOthersOnlyMe_AndCanEdit()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Add(f.Bora, "BoraGizli", Visibility.OnlyMe);

        var model = Assert.IsType<ContactDetailViewModel>(
            Assert.IsType<ViewResult>(f.Controller(f.Mert, RoleNames.Manager).Detail(hidden.ContactId)).Model);

        Assert.True(model.CanEdit);
    }

    [Fact]
    public async Task Detail_UnknownId_Returns404()
    {
        using var f = await Fixture.CreateAsync();
        Assert.IsType<NotFoundResult>(f.Controller(f.Mert, RoleNames.Manager).Detail(9999));
    }

    [Fact]
    public async Task ListItem_HasTelAndWhatsAppLinks()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, "Ali", Visibility.OnlyMe, "0532 111 22 33");

        var item = Assert.IsType<ContactListViewModel>(
            Assert.IsType<ViewResult>(f.Controller(f.Ayse, RoleNames.Agent).Index(null, null)).Model).Items.Single();

        Assert.Equal("tel:+905321112233", item.TelLink);
        Assert.Equal("https://wa.me/905321112233", item.WhatsAppLink);
    }

    [Fact]
    public async Task Index_PageBeyondLast_RedirectsToLastPage()
    {
        using var f = await Fixture.CreateAsync();
        for (var i = 0; i < 25; i++)
            f.Add(f.Ayse, $"Kisi{i:00}", Visibility.OnlyMe);

        var redirect = Assert.IsType<RedirectToActionResult>(f.Controller(f.Ayse, RoleNames.Agent).Index(null, null, 99));
        Assert.Equal(2, redirect.RouteValues!["page"]);
    }

    [Fact]
    public async Task Index_RoleFilter_And_PhoneSearch()
    {
        using var f = await Fixture.CreateAsync();
        var seller = new Contact { Name = "Satici", Surname = "Kisi", Phone = "0100 001 01 07", IsSeller = true };
        f.Service.TCreate(seller, f.Ayse.Id);
        f.Add(f.Ayse, "Alici", Visibility.OnlyMe, "0100 002 02 14"); // Add alıcı yapar

        var controller = f.Controller(f.Ayse, RoleNames.Agent);

        Assert.Equal(new[] { "Satici Kisi" }, Names(controller.Index(null, ContactRole.Seller)));
        Assert.Equal(new[] { "Alici Test" }, Names(controller.Index("0100 002", null)));
    }

    [Fact]
    public async Task Index_HugePageNumber_DoesNotCrash()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, "Ali", Visibility.OnlyMe);

        // int.MaxValue sayfa: taşma olmamalı, son sayfaya yönlenmeli
        Assert.IsType<RedirectToActionResult>(f.Controller(f.Ayse, RoleNames.Agent).Index(null, null, int.MaxValue));
    }

    [Fact]
    public async Task ListItem_InvalidPhone_HasNoLinks()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, "Kisa", Visibility.OnlyMe, "12345");

        var item = Assert.IsType<ContactListViewModel>(
            Assert.IsType<ViewResult>(f.Controller(f.Ayse, RoleNames.Agent).Index(null, null)).Model).Items.Single();

        Assert.False(item.HasPhoneLinks);
        Assert.Equal("", item.TelLink);
        Assert.Equal("", item.WhatsAppLink);
    }

    [Fact]
    public void Seed_FakePhones_UseUnassignedPrefix()
    {
        // Sahte numaralar gerçek bir operatöre gitmemeli (0100 atanmamış önek)
        Assert.StartsWith("0100 ", EmlakOfisi.WebUI.SeedData.TestDataSeeder.FakePhone(1));
        Assert.StartsWith("0100 ", EmlakOfisi.WebUI.SeedData.TestDataSeeder.FakePhone(25));
    }

    [Fact]
    public async Task Index_Paging_20PerPage()
    {
        using var f = await Fixture.CreateAsync();
        for (var i = 0; i < 25; i++)
            f.Add(f.Ayse, $"Kisi{i:00}", Visibility.OnlyMe);

        var controller = f.Controller(f.Ayse, RoleNames.Agent);
        var page1 = Assert.IsType<ContactListViewModel>(Assert.IsType<ViewResult>(controller.Index(null, null, 1)).Model);
        var page2 = Assert.IsType<ContactListViewModel>(Assert.IsType<ViewResult>(controller.Index(null, null, 2)).Model);

        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(2, page1.TotalPages);
    }
}
