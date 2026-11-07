using System.Reflection;
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

// Görev 18: satış/kiralama işlemleri ve komisyon. Ayşe, Bora, Cem: danışman. Mert: yönetici.
// Görme/düzenleme: yönetici, sorumlu danışman ya da işlemi oluşturan (komisyon başkasına görünmez).
public class TransactionTests
{
    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public PropertyManager Properties { get; private init; } = null!;
        public TransactionManager Transactions { get; private init; } = null!;
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
                Transactions = TestControllers.Transactions(identity.Db, contacts, properties),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Cem = await identity.AddUserAsync("cem", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        public bool IsManager(AppUser u) => u == Mert;
        public string RoleOf(AppUser u) => u == Mert ? RoleNames.Manager : RoleNames.Agent;

        public TransactionController As(AppUser u) =>
            Identity.SignedInAs(TestControllers.Transaction(Identity.Db, Contacts, Properties), u, RoleOf(u));

        private int _phone;
        public Contact AddContact(AppUser owner, Visibility visibility, string name = "Alıcı")
        {
            var c = new Contact { Name = name, Surname = "Test", Phone = $"0100{++_phone:D7}", Visibility = visibility, IsBuyer = true, IsTenant = true };
            Contacts.TCreate(c, owner.Id);
            return c;
        }

        public Property AddProperty(AppUser creator, Visibility visibility, ListingType listing = ListingType.Sale, int agent = 0)
        {
            var p = new Property
            {
                Title = "Portföy", City = "İstanbul", District = "Kadıköy", Price = 1_000_000,
                Visibility = visibility, ListingType = listing, AgentUserId = agent
            };
            Assert.Equal(OperationResult.Success, Properties.TCreate(p, creator.Id, IsManager(creator)));
            return p;
        }

        public Transaction Add(AppUser creator, Property p, Contact c, int agent = 0, decimal? agreed = null,
            decimal? rate = null, decimal? amount = null, TransactionStatus status = TransactionStatus.Offer)
        {
            var t = new Transaction
            {
                PropertyId = p.PropertyId, CustomerContactId = c.ContactId, AgentUserId = agent,
                AgreedPrice = agreed, CommissionRate = rate, CommissionAmount = amount, Status = status
            };
            Assert.Equal(OperationResult.Success, Transactions.TCreate(t, creator.Id, IsManager(creator), out _));
            return t;
        }

        public List<TransactionRow> List(AppUser u, TransactionScope? scope = null) =>
            Transactions.TGetVisibleList(u.Id, IsManager(u), new TransactionFilter { Scope = scope }, 1, 100).Items;

        public Transaction Read(int id) => Transactions.TGetVisibleById(id, Mert.Id, true)!.Transaction;

        public void Dispose() => Identity.Dispose();
    }

    private static T ModelOf<T>(IActionResult r) => Assert.IsType<T>(Assert.IsType<ViewResult>(r).Model);

    // ---------------- Görünürlük ----------------

    [Fact]
    public async Task Visibility_OwnAndAssigned_ManagerAll_OthersNothing()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Mert, Visibility.Public);
        var c = f.AddContact(f.Mert, Visibility.Public);
        var ayse = f.Add(f.Ayse, p, c, amount: 100);
        var assignedToCem = f.Add(f.Mert, p, c, agent: f.Cem.Id, amount: 200);

        Assert.Equal(new[] { ayse.TransactionId }, f.List(f.Ayse).Select(r => r.Transaction.TransactionId));
        Assert.Equal(new[] { assignedToCem.TransactionId }, f.List(f.Cem).Select(r => r.Transaction.TransactionId));
        Assert.Empty(f.List(f.Bora));   // portföy ve müşteri herkese açık olsa da başkasının işlemi/komisyonu görünmez
        Assert.Equal(2, f.List(f.Mert).Count);
        Assert.Null(f.Transactions.TGetVisibleById(ayse.TransactionId, f.Bora.Id, false));
    }

    [Fact]
    public async Task LinkedRecords_HiddenFromViewer_AreNotLeaked()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Mert, Visibility.OnlyMe);
        var c = f.AddContact(f.Mert, Visibility.OnlyMe, "Gizli");
        f.Add(f.Mert, p, c, agent: f.Ayse.Id);

        var row = Assert.Single(f.List(f.Ayse));
        Assert.True(row.PropertyHidden);
        Assert.True(row.ContactHidden);
        var item = TransactionItemViewModel.From(row, f.Ayse.Id, true);
        Assert.Null(item.PropertyId);
        Assert.Null(item.PropertyTitle);
        Assert.Null(item.ContactId);
        Assert.Null(item.ContactName);
        Assert.Equal(string.Empty, item.TelLink);
    }

    [Fact]
    public async Task Dashboard_Summary_OnlyVisibleAndThisMonth()
    {
        using var f = await Fixture.CreateAsync();
        var sale = f.AddProperty(f.Mert, Visibility.Public);
        var rent = f.AddProperty(f.Mert, Visibility.Public, ListingType.Rent);
        var c = f.AddContact(f.Mert, Visibility.Public);
        f.Add(f.Ayse, sale, c, amount: 1000, status: TransactionStatus.Completed);
        f.Add(f.Ayse, rent, c, amount: 500, status: TransactionStatus.Completed);
        f.Add(f.Ayse, sale, c, amount: 9999);                                       // tamamlanmadı
        f.Add(f.Bora, sale, c, amount: 7000, status: TransactionStatus.Completed);  // Ayşe görmez

        var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var ayse = f.Transactions.TGetCompletedSummary(f.Ayse.Id, false, from, from.AddMonths(1));
        Assert.Equal((1, 1, 1500m), (ayse.SaleCount, ayse.RentCount, ayse.Commission));

        var mert = f.Transactions.TGetCompletedSummary(f.Mert.Id, true, from, from.AddMonths(1));
        Assert.Equal((2, 1, 8500m), (mert.SaleCount, mert.RentCount, mert.Commission));

        var lastMonth = f.Transactions.TGetCompletedSummary(f.Mert.Id, true, from.AddMonths(-1), from);
        Assert.Equal(0, lastMonth.SaleCount + lastMonth.RentCount);
    }

    // ---------------- Başlatma kuralları ----------------

    [Fact]
    public async Task Create_HiddenProperty_NotFound_HiddenContact_Invalid()
    {
        using var f = await Fixture.CreateAsync();
        var hiddenP = f.AddProperty(f.Bora, Visibility.OnlyMe);
        var openP = f.AddProperty(f.Bora, Visibility.Public);
        var hiddenC = f.AddContact(f.Bora, Visibility.OnlyMe);
        var ownC = f.AddContact(f.Ayse, Visibility.OnlyMe);

        Assert.Equal(OperationResult.NotFound, f.Transactions.TCreate(
            new Transaction { PropertyId = hiddenP.PropertyId, CustomerContactId = ownC.ContactId }, f.Ayse.Id, false, out _));
        Assert.Equal(OperationResult.Invalid, f.Transactions.TCreate(
            new Transaction { PropertyId = openP.PropertyId, CustomerContactId = hiddenC.ContactId }, f.Ayse.Id, false, out _));
        Assert.Empty(f.List(f.Mert));
    }

    [Fact]
    public async Task Create_ServiceSetsProtectedFields_TypeFromProperty()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Ayse, Visibility.OnlyMe, ListingType.Rent);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var t = new Transaction
        {
            TransactionId = 555, PropertyId = p.PropertyId, CustomerContactId = c.ContactId, Type = ListingType.Sale,
            AgentUserId = f.Bora.Id, CreatedByUserId = f.Bora.Id, CreatedAt = new DateTime(2001, 1, 1), CompletedAt = new DateTime(2001, 1, 1)
        };
        Assert.Equal(OperationResult.Success, f.Transactions.TCreate(t, f.Ayse.Id, false, out _));

        var saved = f.Read(t.TransactionId);
        Assert.NotEqual(555, saved.TransactionId);
        Assert.Equal(ListingType.Rent, saved.Type);
        Assert.Equal(f.Ayse.Id, saved.AgentUserId);       // danışman başkası adına başlatamaz
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
        Assert.Null(saved.CompletedAt);
        Assert.True(saved.CreatedAt > DateTime.Now.AddMinutes(-1));
    }

    [Fact]
    public async Task Manager_AssignsAgent_InactiveRejected()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Mert, Visibility.Public);
        var c = f.AddContact(f.Mert, Visibility.Public);
        var passive = await f.Identity.AddUserAsync("pasif", RoleNames.Agent, isActive: false);

        var t = f.Add(f.Mert, p, c, agent: f.Bora.Id);
        Assert.Equal(f.Bora.Id, f.Read(t.TransactionId).AgentUserId);
        Assert.Equal(OperationResult.Invalid, f.Transactions.TCreate(
            new Transaction { PropertyId = p.PropertyId, CustomerContactId = c.ContactId, AgentUserId = passive.Id }, f.Mert.Id, true, out _));
    }

    // ---------------- Komisyon ----------------

    [Theory]
    [InlineData(8_000_000d, 2d, null, 160_000d)]       // fiyat x oran
    [InlineData(8_000_000d, 2.5d, 1d, 200_000d)]       // oran varsa elle tutar yok sayılır
    [InlineData(35_000d, 100d, null, 35_000d)]         // kiralık: bir aylık kira
    [InlineData(null, 2d, 50_000d, 50_000d)]           // fiyat yoksa elle tutar
    [InlineData(1_234_567d, 1.75d, null, 21_604.92d)]  // 2 haneye yuvarlama
    public async Task Commission_IsCalculatedOnServer(double? agreed, double? rate, double? amount, double expected)
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Ayse, Visibility.OnlyMe);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var t = f.Add(f.Ayse, p, c, agreed: (decimal?)agreed, rate: (decimal?)rate, amount: (decimal?)amount);
        Assert.Equal(Math.Round((decimal)expected, 2), f.Read(t.TransactionId).CommissionAmount);
    }

    [Fact]
    public async Task InvalidAmounts_AreInvalid()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Ayse, Visibility.OnlyMe);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        foreach (var bad in new[]
        {
            new Transaction { AgreedPrice = -1 }, new Transaction { CommissionRate = 101 },
            new Transaction { CommissionAmount = -5 }, new Transaction { OfferPrice = -1 }
        })
        {
            bad.PropertyId = p.PropertyId;
            bad.CustomerContactId = c.ContactId;
            Assert.Equal(OperationResult.Invalid, f.Transactions.TCreate(bad, f.Ayse.Id, false, out _));
        }
    }

    // ---------------- Tamamlanma ve portföy durumu ----------------

    [Fact]
    public async Task Complete_SetsDate_AndMarksPropertySoldOrRented()
    {
        using var f = await Fixture.CreateAsync();
        var sale = f.AddProperty(f.Ayse, Visibility.OnlyMe);
        var rent = f.AddProperty(f.Ayse, Visibility.OnlyMe, ListingType.Rent);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var t1 = f.Add(f.Ayse, sale, c);
        var t2 = f.Add(f.Ayse, rent, c);

        Assert.Equal(OperationResult.Success, f.Transactions.TSetStatus(t1.TransactionId, TransactionStatus.Completed, f.Ayse.Id, false, out var u1));
        Assert.Equal(PropertyStatusUpdate.Updated, u1);
        Assert.Equal(PropertyStatus.Sold, f.Properties.TGetVisibleById(sale.PropertyId, f.Mert.Id, true)!.Status);
        Assert.NotNull(f.Read(t1.TransactionId).CompletedAt);

        f.Transactions.TSetStatus(t2.TransactionId, TransactionStatus.Completed, f.Ayse.Id, false, out _);
        Assert.Equal(PropertyStatus.Rented, f.Properties.TGetVisibleById(rent.PropertyId, f.Mert.Id, true)!.Status);

        // Tamamlandıdan çıkınca tarih silinir; portföy geri alınmaz, kontrol uyarısı verilir
        f.Transactions.TSetStatus(t1.TransactionId, TransactionStatus.Agreed, f.Ayse.Id, false, out var u3);
        Assert.Null(f.Read(t1.TransactionId).CompletedAt);
        Assert.Equal(PropertyStatusUpdate.CheckProperty, u3);
    }

    [Fact]
    public async Task Complete_WithoutPropertyEditRight_PropertyUnchanged_Warns()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Bora, Visibility.Public);   // Ayşe görür ama düzenleyemez
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var t = f.Add(f.Ayse, p, c);

        Assert.Equal(OperationResult.Success, f.Transactions.TSetStatus(t.TransactionId, TransactionStatus.Completed, f.Ayse.Id, false, out var update));
        Assert.Equal(PropertyStatusUpdate.NotAllowed, update);
        Assert.Equal(PropertyStatus.Active, f.Properties.TGetVisibleById(p.PropertyId, f.Mert.Id, true)!.Status);
        Assert.Equal(TransactionStatus.Completed, f.Read(t.TransactionId).Status);
    }

    // ---------------- Kapanmış işlem: danışman sadece aynı gün değiştirir ----------------

    private static void MakeClosedYesterday(Fixture f, Transaction t)
    {
        var tracked = f.Identity.Db.Transactions.Single(x => x.TransactionId == t.TransactionId);
        tracked.ClosedAt = DateTime.Today.AddDays(-1).AddHours(15);
        f.Identity.Db.SaveChanges();
    }

    [Fact]
    public async Task ClosedTransaction_AgentSameDayOnly_ManagerAlways()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Ayse, Visibility.OnlyMe);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var t = f.Add(f.Ayse, p, c, amount: 1000, status: TransactionStatus.Completed);
        Assert.NotNull(f.Read(t.TransactionId).ClosedAt);

        // Aynı gün: danışman düzeltebilir
        Assert.True(f.Transactions.TCanEdit(f.Read(t.TransactionId), f.Ayse.Id, false));
        Assert.Equal(OperationResult.Success, f.Transactions.TUpdate(t.TransactionId,
            new Transaction { CommissionAmount = 1200, Status = TransactionStatus.Completed }, f.Ayse.Id, false, out _));

        // Ertesi gün: danışman değiştiremez (ne tutar ne durum), yönetici değiştirebilir
        MakeClosedYesterday(f, t);
        Assert.False(f.Transactions.TCanEdit(f.Read(t.TransactionId), f.Ayse.Id, false));
        Assert.Equal(OperationResult.Forbidden, f.Transactions.TUpdate(t.TransactionId,
            new Transaction { CommissionAmount = 9999, Status = TransactionStatus.Completed }, f.Ayse.Id, false, out _));
        Assert.Equal(OperationResult.Forbidden, f.Transactions.TSetStatus(t.TransactionId, TransactionStatus.Offer, f.Ayse.Id, false, out _));
        Assert.Equal(1200m, f.Read(t.TransactionId).CommissionAmount);
        Assert.Equal(TransactionStatus.Completed, f.Read(t.TransactionId).Status);

        Assert.Equal(OperationResult.Success, f.Transactions.TSetStatus(t.TransactionId, TransactionStatus.Agreed, f.Mert.Id, true, out var update));
        Assert.Equal(PropertyStatusUpdate.CheckProperty, update);   // portföy Satıldı kaldı: kontrol uyarısı
        Assert.Null(f.Read(t.TransactionId).ClosedAt);
        Assert.Null(f.Read(t.TransactionId).CompletedAt);
    }

    [Fact]
    public async Task CancelledTransaction_AlsoLockedNextDay()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Ayse, Visibility.OnlyMe);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var t = f.Add(f.Ayse, p, c);
        f.Transactions.TSetStatus(t.TransactionId, TransactionStatus.Cancelled, f.Ayse.Id, false, out _);
        Assert.True(f.Transactions.TCanEdit(f.Read(t.TransactionId), f.Ayse.Id, false));

        MakeClosedYesterday(f, t);
        Assert.Equal(OperationResult.Forbidden, f.Transactions.TSetStatus(t.TransactionId, TransactionStatus.Completed, f.Ayse.Id, false, out _));
    }

    [Fact]
    public async Task Controller_ClosedYesterday_Agent403()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Ayse, Visibility.OnlyMe);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);
        var t = f.Add(f.Ayse, p, c, status: TransactionStatus.Completed);
        MakeClosedYesterday(f, t);

        Assert.IsType<ForbidResult>(f.As(f.Ayse).Edit(t.TransactionId));
        Assert.IsType<ForbidResult>(f.As(f.Ayse).Edit(t.TransactionId, new TransactionFormViewModel { CustomerContactId = c.ContactId }));
        Assert.IsType<ForbidResult>(f.As(f.Ayse).SetStatus(t.TransactionId, TransactionStatus.Offer));
        Assert.False(ModelOf<TransactionListViewModel>(f.As(f.Ayse).Index(TransactionScope.Completed)).Items.Single().CanEdit);
    }

    // ---------------- Düzenleme ----------------

    [Fact]
    public async Task UpdateAndStatus_NotVisible_NotFound()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Bora, Visibility.Public);
        var c = f.AddContact(f.Bora, Visibility.Public);
        var t = f.Add(f.Bora, p, c);

        Assert.Equal(OperationResult.NotFound, f.Transactions.TUpdate(t.TransactionId, new Transaction(), f.Ayse.Id, false, out _));
        Assert.Equal(OperationResult.NotFound, f.Transactions.TSetStatus(t.TransactionId, TransactionStatus.Cancelled, f.Ayse.Id, false, out _));
        Assert.Equal(TransactionStatus.Offer, f.Read(t.TransactionId).Status);
    }

    [Fact]
    public async Task Update_PropertyFixed_HiddenCustomerPreserved_AgentOnlyByManager()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Mert, Visibility.Public);
        var other = f.AddProperty(f.Mert, Visibility.Public);
        var hiddenC = f.AddContact(f.Mert, Visibility.OnlyMe);
        var t = f.Add(f.Mert, p, hiddenC, agent: f.Ayse.Id);

        Assert.Equal(OperationResult.Success, f.Transactions.TUpdate(t.TransactionId, new Transaction
        {
            PropertyId = other.PropertyId, CustomerContactId = 0, AgentUserId = f.Bora.Id,
            CreatedByUserId = f.Bora.Id, Status = TransactionStatus.Negotiation
        }, f.Ayse.Id, false, out _));

        var saved = f.Read(t.TransactionId);
        Assert.Equal(p.PropertyId, saved.PropertyId);
        Assert.Equal(hiddenC.ContactId, saved.CustomerContactId);
        Assert.Equal(f.Ayse.Id, saved.AgentUserId);
        Assert.Equal(f.Mert.Id, saved.CreatedByUserId);
        Assert.Equal(TransactionStatus.Negotiation, saved.Status);
    }

    // ---------------- Controller ----------------

    [Fact]
    public void Controller_RequiresStaff()
    {
        Assert.Equal(RoleNames.Staff, typeof(TransactionController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public async Task Controller_Create_HiddenProperty404_InvalidMoney_ShowsForm()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.AddProperty(f.Bora, Visibility.OnlyMe);
        var own = f.AddProperty(f.Ayse, Visibility.OnlyMe);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);

        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Create(hidden.PropertyId, (int?)null));
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Create(hidden.PropertyId, new TransactionFormViewModel { CustomerContactId = c.ContactId }));

        foreach (var form in new[]
        {
            new TransactionFormViewModel { CustomerContactId = c.ContactId, AgreedPriceText = "1.5" },
            new TransactionFormViewModel { CustomerContactId = c.ContactId, CommissionRateText = "150" },
            new TransactionFormViewModel { CustomerContactId = c.ContactId, CommissionRateText = "iki" },
            new TransactionFormViewModel { CustomerContactId = 0 }
        })
        {
            var controller = f.As(f.Ayse);
            Assert.IsType<ViewResult>(controller.Create(own.PropertyId, form));
            Assert.False(controller.ModelState.IsValid);
        }
        Assert.Empty(f.List(f.Mert));
    }

    [Fact]
    public async Task Controller_Create_Valid_RateWithComma()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Ayse, Visibility.OnlyMe);
        var c = f.AddContact(f.Ayse, Visibility.OnlyMe);

        var result = f.As(f.Ayse).Create(p.PropertyId, new TransactionFormViewModel
        {
            CustomerContactId = c.ContactId, AgreedPriceText = "8.000.000", CommissionRateText = "2,5", AgentUserId = f.Bora.Id
        });
        Assert.IsType<RedirectToActionResult>(result);
        var saved = f.List(f.Mert).Single().Transaction;
        Assert.Equal(2.5m, saved.CommissionRate);
        Assert.Equal(200_000m, saved.CommissionAmount);
        Assert.Equal(f.Ayse.Id, saved.AgentUserId);
    }

    [Fact]
    public async Task Controller_EditAndStatus_NotVisible404_BeforeValidation()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Bora, Visibility.Public);
        var c = f.AddContact(f.Bora, Visibility.Public);
        var t = f.Add(f.Bora, p, c);

        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Edit(t.TransactionId));
        var controller = f.As(f.Ayse);
        controller.ModelState.AddModelError("x", "geçersiz");
        Assert.IsType<NotFoundResult>(controller.Edit(t.TransactionId, new TransactionFormViewModel()));
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).SetStatus(t.TransactionId, TransactionStatus.Cancelled));
    }

    [Fact]
    public async Task Controller_Index_OnlyOwn_ScopesWork()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Mert, Visibility.Public);
        var c = f.AddContact(f.Mert, Visibility.Public);
        f.Add(f.Ayse, p, c);
        f.Add(f.Ayse, p, c, status: TransactionStatus.Completed);
        f.Add(f.Bora, p, c);

        Assert.Single(ModelOf<TransactionListViewModel>(f.As(f.Ayse).Index()).Items);
        Assert.Single(ModelOf<TransactionListViewModel>(f.As(f.Ayse).Index(TransactionScope.Completed)).Items);
        Assert.Equal(2, ModelOf<TransactionListViewModel>(f.As(f.Mert).Index()).TotalCount);
    }

    [Fact]
    public async Task Dashboard_ShowsMonthSummary()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.AddProperty(f.Mert, Visibility.Public);
        var c = f.AddContact(f.Mert, Visibility.Public);
        f.Add(f.Ayse, p, c, agreed: 1_000_000, rate: 2, status: TransactionStatus.Completed);

        var model = ModelOf<DashboardViewModel>(f.Identity.SignedInAs(
            TestControllers.Dashboard(f.Identity.Db, f.Properties, f.Contacts), f.Ayse, RoleNames.Agent).Index());
        Assert.Equal((1, 0, 20_000m), (model.MonthSaleCount, model.MonthRentCount, model.MonthCommission));

        var bora = ModelOf<DashboardViewModel>(f.Identity.SignedInAs(
            TestControllers.Dashboard(f.Identity.Db, f.Properties, f.Contacts), f.Bora, RoleNames.Agent).Index());
        Assert.Equal(0m, bora.MonthCommission);
    }
}
