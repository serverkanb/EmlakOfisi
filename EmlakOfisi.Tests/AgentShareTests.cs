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

// Görev 18b: danışman primi. Admin danışmana oran yazar; işlem tamamlanınca o anki oran işleme kopyalanır,
// prim = komisyon x oran / 100. Danışman sadece kendi primini görür.
public class AgentShareTests
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
        public AppUser Ada { get; private init; } = null!;
        public Property Property { get; private set; } = null!;
        public Contact Customer { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var contacts = new ContactManager(new EfContactDal(identity.Db));
            var properties = new PropertyManager(new EfPropertyDal(identity.Db), contacts);
            var f = new Fixture
            {
                Identity = identity,
                Contacts = contacts,
                Properties = properties,
                Transactions = TestControllers.Transactions(identity.Db, contacts, properties),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager),
                Ada = await identity.AddUserAsync("ada", RoleNames.Admin)
            };
            f.Property = new Property { Title = "P", City = "İstanbul", District = "Kadıköy", Price = 1, Visibility = Visibility.Public };
            properties.TCreate(f.Property, f.Mert.Id, true);
            f.Customer = new Contact { Name = "Alıcı", Surname = "Test", Phone = "01000000001", Visibility = Visibility.Public, IsBuyer = true };
            contacts.TCreate(f.Customer, f.Mert.Id);
            return f;
        }

        public async Task SetRate(AppUser u, decimal? rate)
        {
            var user = await Identity.UserManager.FindByIdAsync(u.Id.ToString());
            user!.CommissionSharePercent = rate;
            await Identity.UserManager.UpdateAsync(user);
        }

        public Transaction Add(AppUser creator, decimal? commission, TransactionStatus status = TransactionStatus.Offer,
            int agent = 0, decimal? shareRate = null)
        {
            var t = new Transaction
            {
                PropertyId = Property.PropertyId, CustomerContactId = Customer.ContactId, CommissionAmount = commission,
                Status = status, AgentUserId = agent, AgentShareRate = shareRate
            };
            Assert.Equal(OperationResult.Success, Transactions.TCreate(t, creator.Id, creator == Mert, out _));
            return t;
        }

        public Transaction Read(int id) => Transactions.TGetVisibleById(id, Mert.Id, true)!.Transaction;

        public void Dispose() => Identity.Dispose();
    }

    [Fact]
    public async Task Share_IsCalculatedOnCompletion_WithAgentRate()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Ayse, 40);
        var t = f.Add(f.Ayse, 170_000);

        Assert.Null(f.Read(t.TransactionId).AgentShareAmount);   // tamamlanmadan prim yok
        f.Transactions.TSetStatus(t.TransactionId, TransactionStatus.Completed, f.Ayse.Id, false, out _);

        var saved = f.Read(t.TransactionId);
        Assert.Equal(40m, saved.AgentShareRate);
        Assert.Equal(68_000m, saved.AgentShareAmount);
    }

    [Fact]
    public async Task RateSnapshot_DoesNotChange_WhenAdminChangesRateLater()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Ayse, 40);
        var t = f.Add(f.Ayse, 100_000, TransactionStatus.Completed);
        Assert.Equal(40_000m, f.Read(t.TransactionId).AgentShareAmount);

        await f.SetRate(f.Ayse, 50);
        // Aynı gün komisyon düzeltilirse kayıtlı oranla yeniden hesaplanır (yeni oran değil)
        f.Transactions.TUpdate(t.TransactionId, new Transaction { CommissionAmount = 120_000, Status = TransactionStatus.Completed },
            f.Ayse.Id, false, out _);
        Assert.Equal(40m, f.Read(t.TransactionId).AgentShareRate);
        Assert.Equal(48_000m, f.Read(t.TransactionId).AgentShareAmount);

        // Yeni tamamlanan işlem yeni oranı alır
        var t2 = f.Add(f.Ayse, 100_000, TransactionStatus.Completed);
        Assert.Equal(50_000m, f.Read(t2.TransactionId).AgentShareAmount);
    }

    [Fact]
    public async Task NoRate_NoShare()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse, 100_000, TransactionStatus.Completed);
        Assert.Null(f.Read(t.TransactionId).AgentShareRate);
        Assert.Null(f.Read(t.TransactionId).AgentShareAmount);
    }

    [Fact]
    public async Task AgentCannotSetOwnRate_ManagerCanOverride()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Ayse, 40);

        var byAgent = f.Add(f.Ayse, 100_000, TransactionStatus.Completed, shareRate: 90);
        Assert.Equal(40m, f.Read(byAgent.TransactionId).AgentShareRate);   // danışmanın gönderdiği oran yok sayılır

        Assert.Equal(OperationResult.Success, f.Transactions.TUpdate(byAgent.TransactionId,
            new Transaction { CommissionAmount = 100_000, Status = TransactionStatus.Completed, AgentShareRate = 90 },
            f.Ayse.Id, false, out _));
        Assert.Equal(40m, f.Read(byAgent.TransactionId).AgentShareRate);

        var byManager = f.Add(f.Mert, 100_000, TransactionStatus.Completed, agent: f.Ayse.Id, shareRate: 45);
        Assert.Equal(45_000m, f.Read(byManager.TransactionId).AgentShareAmount);

        Assert.Equal(OperationResult.Invalid, f.Transactions.TUpdate(byManager.TransactionId,
            new Transaction { CommissionAmount = 100_000, Status = TransactionStatus.Completed, AgentShareRate = 150 },
            f.Mert.Id, true, out _));
    }

    [Fact]
    public async Task ManagerReassignsAgent_ShareTakesNewAgentsRate()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Ayse, 40);
        await f.SetRate(f.Bora, 30);
        var t = f.Add(f.Mert, 100_000, TransactionStatus.Completed, agent: f.Ayse.Id);
        Assert.Equal(40m, f.Read(t.TransactionId).AgentShareRate);

        // Yönetici sorumluyu değiştirir, oranı elle değiştirmez (formda mevcut oran geri gelir)
        f.Transactions.TUpdate(t.TransactionId, new Transaction
        {
            CommissionAmount = 100_000, Status = TransactionStatus.Completed, AgentUserId = f.Bora.Id, AgentShareRate = 40
        }, f.Mert.Id, true, out _);
        Assert.Equal(30m, f.Read(t.TransactionId).AgentShareRate);
        Assert.Equal(30_000m, f.Read(t.TransactionId).AgentShareAmount);
    }

    [Fact]
    public async Task LeavingCompleted_ClearsShareAmount()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Ayse, 40);
        var t = f.Add(f.Ayse, 100_000, TransactionStatus.Completed);
        f.Transactions.TSetStatus(t.TransactionId, TransactionStatus.Cancelled, f.Ayse.Id, false, out _);
        Assert.Null(f.Read(t.TransactionId).AgentShareAmount);
    }

    [Fact]
    public async Task Dashboard_AgentSeesOwnShare_ManagerSeesTotal()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Ayse, 40);
        await f.SetRate(f.Bora, 50);
        f.Add(f.Ayse, 100_000, TransactionStatus.Completed);
        f.Add(f.Bora, 100_000, TransactionStatus.Completed);

        DashboardViewModel View(AppUser u, string role) => Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(
            f.Identity.SignedInAs(TestControllers.Dashboard(f.Identity.Db, f.Properties, f.Contacts), u, role).Index()).Model);

        Assert.Equal(40_000m, View(f.Ayse, RoleNames.Agent).MonthAgentShare);
        Assert.Equal(50_000m, View(f.Bora, RoleNames.Agent).MonthAgentShare);
        Assert.Equal(90_000m, View(f.Mert, RoleNames.Manager).MonthAgentShare);
    }

    [Fact]
    public async Task Admin_SetsRate_InvalidRejected()
    {
        using var f = await Fixture.CreateAsync();
        UserPermissionController Admin() =>
            f.Identity.SignedInAs(new UserPermissionController(f.Identity.UserManager, f.Identity.SignInManager), f.Ada, RoleNames.Admin);

        await Admin().Edit(f.Ayse.Id, null, "37,5");
        Assert.Equal(37.5m, (await f.Identity.UserManager.FindByIdAsync(f.Ayse.Id.ToString()))!.CommissionSharePercent);

        var page = Assert.IsType<UserPermissionPageViewModel>(Assert.IsType<ViewResult>(await Admin().Edit(f.Ayse.Id)).Model);
        Assert.Equal("37,5", page.CommissionShareText);

        foreach (var bad in new[] { "150", "abc", "-5", "1,234" })
        {
            var result = await Admin().Edit(f.Ayse.Id, null, bad);
            Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(37.5m, (await f.Identity.UserManager.FindByIdAsync(f.Ayse.Id.ToString()))!.CommissionSharePercent);
        }

        await Admin().Edit(f.Ayse.Id, null, "");
        Assert.Null((await f.Identity.UserManager.FindByIdAsync(f.Ayse.Id.ToString()))!.CommissionSharePercent);
    }

    [Fact]
    public async Task OtherAgentsShare_IsNotVisible()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Bora, 50);
        var t = f.Add(f.Bora, 100_000, TransactionStatus.Completed);

        Assert.Null(f.Transactions.TGetVisibleById(t.TransactionId, f.Ayse.Id, false));
        Assert.Empty(f.Transactions.TGetVisibleList(f.Ayse.Id, false, new TransactionFilter(), 1, 100).Items);
    }

    // İşlemi açıp sorumluyu başkasına devreden danışman, yeni sorumlunun primini ve oranını görmez;
    // ana ekran özetine de o işlem girmez.
    [Fact]
    public async Task Creator_NotAgent_DoesNotSeeOthersShare()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Bora, 40);
        await f.SetRate(f.Ayse, 30);
        var t = f.Add(f.Ayse, 100_000);   // Ayşe açar
        f.Transactions.TUpdate(t.TransactionId, new Transaction
        {
            CommissionAmount = 100_000, Status = TransactionStatus.Completed, AgentUserId = f.Bora.Id
        }, f.Mert.Id, true, out _);       // yönetici Bora'ya verip tamamlar
        Assert.Equal(40_000m, f.Read(t.TransactionId).AgentShareAmount);

        var row = f.Transactions.TGetVisibleById(t.TransactionId, f.Ayse.Id, false)!;   // Ayşe (oluşturan) işlemi görür
        var forAyse = TransactionItemViewModel.From(row, f.Ayse.Id, false, canSeeAll: false);
        Assert.False(forAyse.ShowShare);
        Assert.Null(forAyse.AgentShareAmount);
        Assert.Null(forAyse.AgentShareRate);

        var forBora = TransactionItemViewModel.From(f.Transactions.TGetVisibleById(t.TransactionId, f.Bora.Id, false)!, f.Bora.Id, false, false);
        Assert.Equal(40_000m, forBora.AgentShareAmount);

        var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var ayseSummary = f.Transactions.TGetCompletedSummary(f.Ayse.Id, false, from, from.AddMonths(1));
        Assert.Equal((0, 0m, 0m), (ayseSummary.SaleCount, ayseSummary.Commission, ayseSummary.MyAgentShare));
        Assert.Equal(1, f.Transactions.TGetCompletedSummary(f.Bora.Id, false, from, from.AddMonths(1)).SaleCount);
    }

    [Fact]
    public async Task Controller_AgentEditForm_HasNoShareRate()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Ayse, 40);
        var t = f.Add(f.Ayse, 100_000, TransactionStatus.Completed);

        var agent = f.Identity.SignedInAs(TestControllers.Transaction(f.Identity.Db, f.Contacts, f.Properties), f.Ayse, RoleNames.Agent);
        var model = Assert.IsType<TransactionFormViewModel>(Assert.IsType<ViewResult>(agent.Edit(t.TransactionId)).Model);
        Assert.Null(model.AgentShareRateText);

        // Danışman düzenlemede oran gönderse de yok sayılır
        var agent2 = f.Identity.SignedInAs(TestControllers.Transaction(f.Identity.Db, f.Contacts, f.Properties), f.Ayse, RoleNames.Agent);
        agent2.Edit(t.TransactionId, new TransactionFormViewModel
        {
            CustomerContactId = f.Customer.ContactId, Status = TransactionStatus.Completed, CommissionAmountText = "100000", AgentShareRateText = "90"
        });
        Assert.Equal(40m, f.Read(t.TransactionId).AgentShareRate);

        var manager = f.Identity.SignedInAs(TestControllers.Transaction(f.Identity.Db, f.Contacts, f.Properties), f.Mert, RoleNames.Manager);
        Assert.Equal("40", Assert.IsType<TransactionFormViewModel>(Assert.IsType<ViewResult>(manager.Edit(t.TransactionId)).Model).AgentShareRateText);
    }

    [Fact]
    public async Task Controller_AgentPostedShareRate_IsIgnored()
    {
        using var f = await Fixture.CreateAsync();
        await f.SetRate(f.Ayse, 40);
        var controller = f.Identity.SignedInAs(TestControllers.Transaction(f.Identity.Db, f.Contacts, f.Properties), f.Ayse, RoleNames.Agent);

        controller.Create(f.Property.PropertyId, new TransactionFormViewModel
        {
            CustomerContactId = f.Customer.ContactId, Status = TransactionStatus.Completed,
            CommissionAmountText = "100000", AgentShareRateText = "99"
        });
        var saved = f.Transactions.TGetVisibleList(f.Mert.Id, true, new TransactionFilter(), 1, 10).Items.Single().Transaction;
        Assert.Equal(40m, saved.AgentShareRate);
        Assert.Equal(40_000m, saved.AgentShareAmount);
    }
}
