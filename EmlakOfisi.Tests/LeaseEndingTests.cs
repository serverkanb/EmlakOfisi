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

// Görev 23: biten kira kontratları. Tamamlanmış kiralamalar; bitişi önümüzdeki 60 gün içinde ya da son 30 günde.
// Danışman sadece sorumlusu olduğu işlemleri, yönetici tüm ofisi görür. Ayşe, Bora: danışman. Mert: yönetici.
public class LeaseEndingTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public PropertyManager Properties { get; private init; } = null!;
        public TransactionManager Transactions { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
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
                Transactions = TestControllers.Transactions(identity.Db, contacts, properties),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        private int _phone;

        // Yönetici kayıtları açar ve işlemi verilen danışmana atar; durum ve bitiş tarihi doğrudan yazılır
        public Transaction Add(AppUser agent, int? endInDays, ListingType listing = ListingType.Rent,
            TransactionStatus status = TransactionStatus.Completed,
            Visibility propertyVisibility = Visibility.Public, Visibility contactVisibility = Visibility.Public)
        {
            var c = new Contact { Name = "Kiracı", Surname = "Test", Phone = $"0100{++_phone:D7}", Visibility = contactVisibility, IsTenant = true, IsBuyer = true };
            Contacts.TCreate(c, Mert.Id);
            var p = new Property { Title = "Daire " + _phone, City = "İzmir", District = "Bornova", Price = 1, Visibility = propertyVisibility, ListingType = listing };
            Assert.Equal(OperationResult.Success, Properties.TCreate(p, Mert.Id, true));

            var t = new Transaction { PropertyId = p.PropertyId, CustomerContactId = c.ContactId, AgentUserId = agent.Id };
            Assert.Equal(OperationResult.Success, Transactions.TCreate(t, Mert.Id, true, out _));

            var tracked = Identity.Db.Transactions.Single(x => x.TransactionId == t.TransactionId);
            tracked.Status = status;
            tracked.LeaseEndDate = endInDays == null ? null : DateTime.Today.AddDays(endInDays.Value);
            Identity.Db.SaveChanges();
            return t;
        }

        public List<int> Ending(AppUser u, bool mine = false) =>
            Transactions.TGetVisibleList(u.Id, u == Mert,
                    new TransactionFilter { Scope = TransactionScope.LeaseEnding, OnlyMine = mine }, 1, 100)
                .Items.Select(r => r.Transaction.TransactionId).ToList();

        public DashboardViewModel Dashboard(AppUser u)
        {
            var c = Identity.SignedInAs(TestControllers.Dashboard(Identity.Db, Properties, Contacts), u,
                u == Mert ? RoleNames.Manager : RoleNames.Agent);
            return Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(c.Index()).Model);
        }

        public TransactionListViewModel ListTab(AppUser u)
        {
            var c = Identity.SignedInAs(TestControllers.Transaction(Identity.Db, Contacts, Properties), u,
                u == Mert ? RoleNames.Manager : RoleNames.Agent);
            return Assert.IsType<TransactionListViewModel>(Assert.IsType<ViewResult>(c.Index(TransactionScope.LeaseEnding)).Model);
        }

        public void Dispose() => Identity.Dispose();
    }

    [Fact]
    public async Task Window_Boundaries_60DaysAhead_30DaysBack()
    {
        using var f = await Fixture.CreateAsync();
        var today = f.Add(f.Ayse, 0);
        var day60 = f.Add(f.Ayse, 60);
        f.Add(f.Ayse, 61);                 // dışarıda
        var back30 = f.Add(f.Ayse, -30);
        f.Add(f.Ayse, -31);                // dışarıda
        f.Add(f.Ayse, null);               // bitiş tarihi yok

        Assert.Equal(new[] { back30.TransactionId, today.TransactionId, day60.TransactionId }, f.Ending(f.Ayse));   // en yakın bitiş üstte
    }

    [Fact]
    public async Task OnlyCompletedRentals()
    {
        using var f = await Fixture.CreateAsync();
        var ok = f.Add(f.Ayse, 10);
        f.Add(f.Ayse, 10, status: TransactionStatus.Agreed);
        f.Add(f.Ayse, 10, status: TransactionStatus.Cancelled);

        // Satışta kira tarihi olamaz; veritabanında elle yazılsa bile listeye girmez
        f.Add(f.Ayse, 10, listing: ListingType.Sale);

        Assert.Equal(new[] { ok.TransactionId }, f.Ending(f.Ayse));
    }

    [Fact]
    public async Task Agent_SeesOnlyOwnAsAgent_ManagerSeesAll()
    {
        using var f = await Fixture.CreateAsync();
        var ayse = f.Add(f.Ayse, 5);
        var bora = f.Add(f.Bora, 7);

        Assert.Equal(new[] { ayse.TransactionId }, f.Ending(f.Ayse));
        Assert.Equal(new[] { bora.TransactionId }, f.Ending(f.Bora));
        Assert.Equal(new[] { ayse.TransactionId, bora.TransactionId }, f.Ending(f.Mert));
        Assert.Empty(f.Ending(f.Mert, mine: true));   // yöneticinin "sadece benim" filtresi
    }

    [Fact]
    public async Task CreatorWhoReassigned_DoesNotSeeIt()
    {
        using var f = await Fixture.CreateAsync();
        var c = new Contact { Name = "K", Surname = "T", Phone = "01009999999", Visibility = Visibility.Public, IsTenant = true };
        f.Contacts.TCreate(c, f.Mert.Id);
        var p = new Property { Title = "D", City = "İzmir", District = "Bornova", Price = 1, Visibility = Visibility.Public, ListingType = ListingType.Rent };
        f.Properties.TCreate(p, f.Mert.Id, true);
        var t = new Transaction { PropertyId = p.PropertyId, CustomerContactId = c.ContactId };
        f.Transactions.TCreate(t, f.Ayse.Id, false, out _);   // Ayşe oluşturdu
        var tracked = f.Identity.Db.Transactions.Single(x => x.TransactionId == t.TransactionId);
        tracked.AgentUserId = f.Bora.Id;                       // yönetici Bora'ya verdi
        tracked.Status = TransactionStatus.Completed;
        tracked.LeaseEndDate = DateTime.Today.AddDays(3);
        f.Identity.Db.SaveChanges();

        Assert.Empty(f.Ending(f.Ayse));
        Assert.Equal(new[] { t.TransactionId }, f.Ending(f.Bora));
    }

    [Fact]
    public async Task HiddenPropertyAndTenant_NotLeaked()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, 5, propertyVisibility: Visibility.OnlyMe, contactVisibility: Visibility.OnlyMe);   // Mert'in gizli kayıtları

        var item = Assert.Single(f.Dashboard(f.Ayse).LeaseEnding);
        Assert.True(item.PropertyHidden);
        Assert.True(item.ContactHidden);
        Assert.Null(item.PropertyTitle);
        Assert.Null(item.ContactName);
        Assert.Null(item.ContactId);
        Assert.Equal(string.Empty, item.TelLink);
    }

    [Fact]
    public async Task Dashboard_CountMatchesTab_ShowsFirstFive()
    {
        using var f = await Fixture.CreateAsync();
        for (var i = 1; i <= 7; i++) f.Add(f.Ayse, i);
        f.Add(f.Bora, 2);

        var d = f.Dashboard(f.Ayse);
        Assert.Equal(7, d.LeaseEndingCount);
        Assert.Equal(DashboardController.UpcomingShown, d.LeaseEnding.Count);
        Assert.Equal(DateTime.Today.AddDays(1), d.LeaseEnding[0].LeaseEndDate);
        Assert.Equal(7, f.ListTab(f.Ayse).TotalCount);

        Assert.Equal(8, f.Dashboard(f.Mert).LeaseEndingCount);
        Assert.Equal(8, f.ListTab(f.Mert).TotalCount);
    }

    [Fact]
    public async Task Filter_WithoutRange_ReturnsNothing()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, 5);
        var dal = new EfTransactionDal(f.Identity.Db);
        Assert.Empty(dal.GetVisibleList(f.Mert.Id, true, new TransactionFilter { Scope = TransactionScope.LeaseEnding }, 0, 10));
    }

    [Fact]
    public async Task Service_OverridesWideRangeFromCaller()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, 200);
        f.Add(f.Ayse, -200);
        var filter = new TransactionFilter
        {
            Scope = TransactionScope.LeaseEnding,
            LeaseEndFrom = DateTime.Today.AddYears(-10),
            LeaseEndTo = DateTime.Today.AddYears(10)
        };
        Assert.Empty(f.Transactions.TGetVisibleList(f.Mert.Id, true, filter, 1, 100).Items);
        Assert.Equal(DateTime.Today.AddDays(ITransactionService.LeaseEndingDays), filter.LeaseEndTo);
    }

    [Theory]
    [InlineData(-3, "Kira bitti (3 gün önce)", "bg-red-lt")]
    [InlineData(0, "Kira bugün bitiyor", "bg-red-lt")]
    [InlineData(12, "Kiranın bitmesine 12 gün", "bg-orange-lt")]
    [InlineData(60, "Kiranın bitmesine 60 gün", "bg-orange-lt")]
    public void LeaseBadge_Texts(int days, string text, string css)
    {
        Assert.Equal((text, css), TransactionLabels.LeaseBadge(ListingType.Rent, TransactionStatus.Completed, DateTime.Today.AddDays(days)));
    }

    [Fact]
    public void LeaseBadge_NoneOutsideWindowOrNotCompletedRent()
    {
        Assert.Null(TransactionLabels.LeaseBadge(ListingType.Rent, TransactionStatus.Completed, DateTime.Today.AddDays(61)));
        Assert.Null(TransactionLabels.LeaseBadge(ListingType.Rent, TransactionStatus.Completed, DateTime.Today.AddDays(-31)));
        Assert.Null(TransactionLabels.LeaseBadge(ListingType.Rent, TransactionStatus.Agreed, DateTime.Today));
        Assert.Null(TransactionLabels.LeaseBadge(ListingType.Sale, TransactionStatus.Completed, DateTime.Today));
        Assert.Null(TransactionLabels.LeaseBadge(ListingType.Rent, TransactionStatus.Completed, null));
    }
}
