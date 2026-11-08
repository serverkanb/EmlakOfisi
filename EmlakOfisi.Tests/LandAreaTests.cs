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

// Arsa düzenlemesi (kullanıcı isteği): arsada oda sayısı, kat ve bina yaşı tutulmaz; talepte "en az alan (m²)",
// portföy listesinde "en az alan" filtresi, "Uygun portföyler" alanı da hesaba katar. 1 dönüm = 1.000 m².
public class LandAreaTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public PropertyManager Properties { get; private init; } = null!;
        public CustomerRequestManager Requests { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public Contact Customer { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var contacts = new ContactManager(new EfContactDal(identity.Db));
            var f = new Fixture
            {
                Identity = identity,
                Contacts = contacts,
                Properties = new PropertyManager(new EfPropertyDal(identity.Db), contacts),
                Requests = TestControllers.Requests(identity.Db, contacts),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent)
            };
            f.Customer = new Contact { Name = "Alıcı", Surname = "Test", Phone = "01000000001", IsBuyer = true };
            contacts.TCreate(f.Customer, f.Ayse.Id);
            return f;
        }

        public Property AddProperty(PropertyType type, int? net = null, int? gross = null, string? rooms = "3+1",
            int? floor = 2, int? age = 5)
        {
            var p = new Property
            {
                Title = "P", City = "İzmir", District = "Urla", Price = 1_000_000, PropertyType = type,
                NetArea = net, GrossArea = gross, RoomCount = rooms, Floor = floor, BuildingAge = age
            };
            Assert.Equal(OperationResult.Success, Properties.TCreate(p, Ayse.Id, false));
            return p;
        }

        public Property Read(int id) => Properties.TGetVisibleById(id, Ayse.Id, false)!;

        public void Dispose() => Identity.Dispose();
    }

    [Fact]
    public async Task Property_Land_ClearsRoomFloorAndAge_OnCreateAndUpdate()
    {
        using var f = await Fixture.CreateAsync();
        var land = f.AddProperty(PropertyType.Land, gross: 5000);
        var saved = f.Read(land.PropertyId);
        Assert.Null(saved.RoomCount);
        Assert.Null(saved.Floor);
        Assert.Null(saved.BuildingAge);
        Assert.Equal(5000, saved.GrossArea);

        // Konut olarak açılıp arsaya çevrilirse de temizlenir
        var house = f.AddProperty(PropertyType.Residence, net: 120);
        Assert.Equal("3+1", f.Read(house.PropertyId).RoomCount);
        var changes = f.Read(house.PropertyId);
        changes.PropertyType = PropertyType.Land;
        Assert.Equal(OperationResult.Success, f.Properties.TUpdate(house.PropertyId, changes, f.Ayse.Id, false));
        Assert.Null(f.Read(house.PropertyId).RoomCount);
        Assert.Null(f.Read(house.PropertyId).Floor);
    }

    [Fact]
    public async Task Request_Land_ClearsRoomCount_KeepsMinArea()
    {
        using var f = await Fixture.CreateAsync();
        var r = new CustomerRequest { PropertyType = PropertyType.Land, RoomCount = "3+1", MinArea = 5000 };
        Assert.Equal(OperationResult.Success, f.Requests.TCreate(f.Customer.ContactId, r, f.Ayse.Id, false, false));

        var saved = f.Requests.TGetVisibleById(r.CustomerRequestId, f.Ayse.Id, false)!;
        Assert.Null(saved.RoomCount);
        Assert.Equal(5000, saved.MinArea);

        Assert.Equal(OperationResult.Success, f.Requests.TUpdate(r.CustomerRequestId,
            new CustomerRequest { PropertyType = PropertyType.Land, RoomCount = "2+1", MinArea = 8000 }, f.Ayse.Id, false, false));
        saved = f.Requests.TGetVisibleById(r.CustomerRequestId, f.Ayse.Id, false)!;
        Assert.Null(saved.RoomCount);
        Assert.Equal(8000, saved.MinArea);

        Assert.Equal(OperationResult.Invalid, f.Requests.TUpdate(r.CustomerRequestId,
            new CustomerRequest { MinArea = 0 }, f.Ayse.Id, false, false));
    }

    [Fact]
    public async Task RequestController_LandForm_DropsRoomCount()
    {
        using var f = await Fixture.CreateAsync();
        var c = f.Identity.SignedInAs(TestControllers.Request(f.Identity.Db, f.Contacts), f.Ayse, RoleNames.Agent);
        Assert.IsType<RedirectToActionResult>(c.Create(f.Customer.ContactId, new CustomerRequestFormViewModel
        {
            PropertyType = PropertyType.Land, RoomCount = "3+1", MinArea = 3000, City = "İzmir"
        }));
        var saved = f.Requests.TGetVisibleList(f.Ayse.Id, false, new CustomerRequestFilter(), 1, 10).Items.Single().Request;
        Assert.Null(saved.RoomCount);
        Assert.Equal(3000, saved.MinArea);
    }

    [Fact]
    public async Task PropertyList_MinAreaFilter_UsesNetThenGross()
    {
        using var f = await Fixture.CreateAsync();
        var small = f.AddProperty(PropertyType.Land, gross: 800);
        var big = f.AddProperty(PropertyType.Land, gross: 6000);
        var house = f.AddProperty(PropertyType.Residence, net: 150, gross: 7000);   // net önceliklidir

        var ids = f.Properties.TGetVisibleList(f.Ayse.Id, false, new PropertyFilter { MinArea = 5000 }, 1, 100).Items
            .Select(p => p.PropertyId).ToList();
        Assert.Equal(new[] { big.PropertyId }, ids);
        _ = small; _ = house;
    }

    [Fact]
    public void MatchRoute_AndSummary_IncludeArea()
    {
        var item = new CustomerRequestItemViewModel
        {
            ListingType = ListingType.Sale, PropertyType = PropertyType.Land, City = "İzmir", District = "Urla", MinArea = 5000
        };
        Assert.Equal("5000", item.MatchRoute["minArea"]);
        Assert.Equal("Satılık · Arsa · Urla / İzmir · en az 5.000 m² (5 dönüm)", item.Summary);
    }

    [Theory]
    [InlineData(850, "850 m²")]
    [InlineData(1000, "1.000 m² (1 dönüm)")]
    [InlineData(2500, "2.500 m² (2,5 dönüm)")]
    [InlineData(12345, "12.345 m² (12,35 dönüm)")]
    public void Area_ShowsDonum(int m2, string expected)
    {
        Assert.Equal(expected, PropertyLabels.Area(m2));
    }
}
