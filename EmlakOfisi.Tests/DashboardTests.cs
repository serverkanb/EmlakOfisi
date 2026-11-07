using System.Reflection;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Görev 15: ana ekran sayıları. Herkes sadece GÖREBİLDİĞİ kayıtları sayar.
// Ayşe, Bora, Cem: danışman. Mert: yönetici.
public class DashboardTests
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

        public DashboardViewModel View(AppUser user, string role)
        {
            var controller = Identity.SignedInAs(new DashboardController(Properties, Contacts), user, role);
            return Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(controller.Index()).Model);
        }

        public Property AddProperty(AppUser creator, Visibility visibility, PropertyStatus status = PropertyStatus.Active,
            ListingType listing = ListingType.Sale, int agent = 0)
        {
            var p = new Property
            {
                Title = "Portföy", City = "İstanbul", District = "Kadıköy", Price = 1_000_000,
                Visibility = visibility, Status = status, ListingType = listing, AgentUserId = agent
            };
            Assert.Equal(BusinessLayer.Abstract.OperationResult.Success,
                Properties.TCreate(p, creator.Id, creator == Mert));
            return p;
        }

        private int _phone;
        public Contact AddContact(AppUser owner, Visibility visibility)
        {
            var c = new Contact
            {
                Name = "Kişi", Surname = "Test", Phone = $"0100{++_phone:D7}", Visibility = visibility, IsBuyer = true
            };
            Contacts.TCreate(c, owner.Id);
            return c;
        }

        public void Dispose() => Identity.Dispose();
    }

    [Fact]
    public void DashboardController_RequiresStaffRole()
    {
        Assert.Equal(RoleNames.Staff, typeof(DashboardController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public async Task Contacts_CountedByVisibility()
    {
        using var f = await Fixture.CreateAsync();
        f.AddContact(f.Ayse, Visibility.OnlyMe);
        f.AddContact(f.Ayse, Visibility.Public);
        f.AddContact(f.Bora, Visibility.OnlyMe);
        f.AddContact(f.Bora, Visibility.OnlyMe);
        f.AddContact(f.Bora, Visibility.Public);

        Assert.Equal(3, f.View(f.Ayse, RoleNames.Agent).Contacts);   // kendi 2 + Bora'nın açığı
        Assert.Equal(4, f.View(f.Bora, RoleNames.Agent).Contacts);   // kendi 3 + Ayşe'nin açığı
        Assert.Equal(2, f.View(f.Cem, RoleNames.Agent).Contacts);    // sadece açıklar
        Assert.Equal(5, f.View(f.Mert, RoleNames.Manager).Contacts); // hepsi
    }

    [Fact]
    public async Task Contacts_DeletedNotCounted()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.AddContact(f.Ayse, Visibility.Public);
        f.AddContact(f.Ayse, Visibility.Public);
        Assert.Equal(BusinessLayer.Abstract.OperationResult.Success, f.Contacts.TSoftDelete(c.ContactId, f.Ayse.Id, false));

        Assert.Equal(1, f.View(f.Ayse, RoleNames.Agent).Contacts);
        Assert.Equal(1, f.View(f.Mert, RoleNames.Manager).Contacts);
    }

    [Fact]
    public async Task Properties_CountedByVisibility_IncludingAssigned()
    {
        using var f = await Fixture.CreateAsync();
        f.AddProperty(f.Ayse, Visibility.OnlyMe);
        f.AddProperty(f.Bora, Visibility.OnlyMe);
        f.AddProperty(f.Bora, Visibility.Public);
        f.AddProperty(f.Mert, Visibility.OnlyMe, agent: f.Cem.Id); // Cem sorumlu

        Assert.Equal(2, f.View(f.Ayse, RoleNames.Agent).ActiveProperties);
        Assert.Equal(2, f.View(f.Bora, RoleNames.Agent).ActiveProperties);
        Assert.Equal(2, f.View(f.Cem, RoleNames.Agent).ActiveProperties);  // açık + atanan
        Assert.Equal(4, f.View(f.Mert, RoleNames.Manager).ActiveProperties);
    }

    [Fact]
    public async Task Properties_StatusAndListingBreakdown()
    {
        using var f = await Fixture.CreateAsync();
        f.AddProperty(f.Mert, Visibility.Public, PropertyStatus.Active, ListingType.Sale);
        f.AddProperty(f.Mert, Visibility.Public, PropertyStatus.Active, ListingType.Sale);
        f.AddProperty(f.Mert, Visibility.Public, PropertyStatus.Active, ListingType.Rent);
        f.AddProperty(f.Mert, Visibility.Public, PropertyStatus.Passive, ListingType.Sale);
        f.AddProperty(f.Mert, Visibility.Public, PropertyStatus.Sold, ListingType.Sale);
        f.AddProperty(f.Mert, Visibility.Public, PropertyStatus.Rented, ListingType.Rent);
        f.AddProperty(f.Mert, Visibility.Public, PropertyStatus.Rented, ListingType.Rent);
        f.AddProperty(f.Mert, Visibility.OnlyMe, PropertyStatus.Active, ListingType.Rent); // danışmana görünmez

        var agent = f.View(f.Ayse, RoleNames.Agent);
        Assert.Equal((3, 2, 1, 1, 1, 2), (agent.ActiveProperties, agent.ActiveSale, agent.ActiveRent,
            agent.PassiveProperties, agent.SoldProperties, agent.RentedProperties));

        var manager = f.View(f.Mert, RoleNames.Manager);
        Assert.Equal((4, 2, 2), (manager.ActiveProperties, manager.ActiveSale, manager.ActiveRent));
        Assert.True(manager.CanSeeAll);
        Assert.False(agent.CanSeeAll);
    }

    [Fact]
    public async Task Properties_DeletedNotCounted()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Ayse, Visibility.Public);
        f.AddProperty(f.Ayse, Visibility.Public, PropertyStatus.Sold);
        Assert.Equal(BusinessLayer.Abstract.OperationResult.Success, f.Properties.TSoftDelete(p.PropertyId, f.Ayse.Id, false));

        var m = f.View(f.Mert, RoleNames.Manager);
        Assert.Equal(0, m.ActiveProperties);
        Assert.Equal(1, m.SoldProperties);
    }

    [Fact]
    public async Task EmptyDatabase_AllZero()
    {
        using var f = await Fixture.CreateAsync();
        var m = f.View(f.Ayse, RoleNames.Agent);
        Assert.Equal(0, m.ActiveProperties + m.PassiveProperties + m.SoldProperties + m.RentedProperties + m.Contacts);
    }
}
