using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.Tests;

// Görev 11: portföy görünürlüğü ve yetkileri (docs/MIMARI_PLAN.md bölüm 3).
// Ayşe, Bora, Cem: danışman. Mert: yönetici (canSeeAll).
// Fark: müşteriden farklı olarak SORUMLU DANIŞMAN da kaydı görür ve düzenler (ama silemez).
public class PropertyVisibilityTests : IDisposable
{
    private const int Ayse = 1;
    private const int Bora = 2;
    private const int Cem = 3;
    private const int Mert = 4;
    private const int Pasif = 5;   // rolü var ama pasif
    private const int Rolsuz = 6;  // aktif ama rolü yok

    private readonly AppDbContext _context;
    private readonly ContactManager _contacts;
    private readonly PropertyManager _service;

    private readonly Property _ayseOnlyMe;
    private readonly Property _aysePublic;
    private readonly Property _boraOnlyMe;
    private readonly Property _mertOnlyMeAssignedToCem; // Mert oluşturdu, sorumlusu Cem
    private readonly Property _boraDeletedPublic;

    public PropertyVisibilityTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new AppDbContext(options);

        // Liste/detay sorumlu danışmanı (AgentUser) birlikte yükler: zorunlu ilişki olduğu için kullanıcı
        // satırları bulunmalı (gerçek veritabanında yabancı anahtar bunu zaten garanti eder).
        // Sorumlu danışman olabilmek için kullanıcı aktif ve rolü olan bir çalışan olmalı (IsActiveStaff)
        _context.Roles.Add(new AppRole { Id = 1, Name = "Agent" });
        foreach (var (id, name) in new[] { (Ayse, "ayse"), (Bora, "bora"), (Cem, "cem"), (Mert, "mert") })
        {
            _context.Users.Add(new AppUser { Id = id, UserName = name, Name = name, Surname = "Test" });
            _context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = id, RoleId = 1 });
        }
        _context.Users.Add(new AppUser { Id = Pasif, UserName = "pasif", Name = "p", Surname = "T", IsActive = false });
        _context.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<int> { UserId = Pasif, RoleId = 1 });
        _context.Users.Add(new AppUser { Id = Rolsuz, UserName = "rolsuz", Name = "r", Surname = "T" });
        _context.SaveChanges();

        _contacts = new ContactManager(new EfContactDal(_context));
        _service = new PropertyManager(new EfPropertyDal(_context), _contacts);

        _ayseOnlyMe = Add("Kadıköy 3+1 Daire", Ayse, false, Visibility.OnlyMe, price: 5_000_000, city: "İstanbul", district: "Kadıköy");
        _aysePublic = Add("Moda Kiralık Daire", Ayse, false, Visibility.Public, ListingType.Rent, price: 40_000, city: "İstanbul", district: "Kadıköy");
        _boraOnlyMe = Add("Gizli Bahçeli Villa", Bora, false, Visibility.OnlyMe, price: 20_000_000, city: "İzmir", district: "Urla");
        _mertOnlyMeAssignedToCem = Add("Yönetici Arsası", Mert, true, Visibility.OnlyMe, type: PropertyType.Land, agent: Cem, price: 3_000_000, city: "Ankara", district: "Gölbaşı");
        _boraDeletedPublic = Add("Silinmiş İş Yeri", Bora, false, Visibility.Public, type: PropertyType.Commercial, price: 1_000_000, city: "Bursa", district: "Nilüfer");
        _service.TSoftDelete(_boraDeletedPublic.PropertyId, Bora, false);
    }

    private Property Add(string title, int user, bool canSeeAll, Visibility visibility,
        ListingType listing = ListingType.Sale, PropertyType type = PropertyType.Residence, int agent = 0,
        decimal price = 1_000_000, string city = "İstanbul", string district = "Kadıköy", int? ownerId = null)
    {
        var p = new Property
        {
            Title = title, ListingType = listing, PropertyType = type, Price = price,
            City = city, District = district, Visibility = visibility, AgentUserId = agent, OwnerContactId = ownerId
        };
        Assert.Equal(OperationResult.Success, _service.TCreate(p, user, canSeeAll));
        return p;
    }

    private List<string> Titles(int user, bool canSeeAll, PropertyFilter? filter = null) =>
        _service.TGetVisibleList(user, canSeeAll, filter ?? new PropertyFilter(), 1, 100)
            .Items.Select(p => p.Title).OrderBy(t => t).ToList();

    public void Dispose() => _context.Dispose();

    // ---------------- Liste ----------------

    [Fact]
    public void Creator_SeesOwnAndPublic_NotOthersHidden()
    {
        Assert.Equal(new[] { "Kadıköy 3+1 Daire", "Moda Kiralık Daire" }, Titles(Ayse, false));
    }

    [Fact]
    public void AssignedAgent_SeesHiddenPropertyAssignedToThem()
    {
        Assert.Equal(new[] { "Moda Kiralık Daire", "Yönetici Arsası" }, Titles(Cem, false));
    }

    [Fact]
    public void OtherAgent_DoesNotSeeHiddenProperties()
    {
        Assert.Equal(new[] { "Gizli Bahçeli Villa", "Moda Kiralık Daire" }, Titles(Bora, false));
    }

    [Fact]
    public void Manager_SeesAllNonDeleted()
    {
        Assert.Equal(4, Titles(Mert, true).Count);
        Assert.DoesNotContain("Silinmiş İş Yeri", Titles(Mert, true));
    }

    // ---------------- Detay ----------------

    [Fact]
    public void GetById_HiddenForOtherAgent_IsNull()
    {
        Assert.Null(_service.TGetVisibleById(_boraOnlyMe.PropertyId, Ayse, false));
        Assert.Null(_service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Ayse, false));
    }

    [Fact]
    public void GetById_AssignedAgent_And_Manager_SeeHidden()
    {
        Assert.NotNull(_service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Cem, false));
        Assert.NotNull(_service.TGetVisibleById(_boraOnlyMe.PropertyId, Mert, true));
    }

    [Fact]
    public void GetById_Deleted_IsNull_EvenForManager()
    {
        Assert.Null(_service.TGetVisibleById(_boraDeletedPublic.PropertyId, Mert, true));
    }

    // ---------------- Arama ve filtreler ----------------

    [Theory]
    [InlineData("Villa")]
    [InlineData("Urla")]
    [InlineData("İzmir")]
    [InlineData("Arsa")]
    [InlineData("Gölbaşı")]
    public void Search_DoesNotLeakHiddenProperties(string search)
    {
        Assert.Empty(Titles(Ayse, false, new PropertyFilter { Search = search }));
    }

    [Fact]
    public void Search_ByTitleCityDistrict()
    {
        Assert.Equal(new[] { "Gizli Bahçeli Villa" }, Titles(Mert, true, new PropertyFilter { Search = "Urla" }));
        Assert.Equal(2, Titles(Mert, true, new PropertyFilter { Search = "Kadıköy" }).Count);
        Assert.Equal(new[] { "Moda Kiralık Daire" }, Titles(Mert, true, new PropertyFilter { Search = "Moda" }));
    }

    [Fact]
    public void Filters_ListingType_PropertyType_Status_Price_OnlyMine()
    {
        Assert.Equal(new[] { "Moda Kiralık Daire" }, Titles(Mert, true, new PropertyFilter { ListingType = ListingType.Rent }));
        Assert.Equal(new[] { "Yönetici Arsası" }, Titles(Mert, true, new PropertyFilter { PropertyType = PropertyType.Land }));
        Assert.Equal(4, Titles(Mert, true, new PropertyFilter { Status = PropertyStatus.Active }).Count);
        Assert.Equal(new[] { "Kadıköy 3+1 Daire", "Yönetici Arsası" },
            Titles(Mert, true, new PropertyFilter { MinPrice = 2_000_000, MaxPrice = 10_000_000 }));
        Assert.Equal(new[] { "Yönetici Arsası" }, Titles(Cem, false, new PropertyFilter { OnlyMine = true }));
    }

    [Fact]
    public void Filters_DoNotBypassVisibility()
    {
        // Bora'nın gizli villası 20M; Ayşe yüksek fiyat filtresiyle bile göremez
        Assert.Empty(Titles(Ayse, false, new PropertyFilter { MinPrice = 15_000_000 }));
    }

    [Fact]
    public void Paging_ReturnsPageAndTotal_AndPageSizeIsCapped()
    {
        var page1 = _service.TGetVisibleList(Mert, true, new PropertyFilter(), 1, 3);
        var page2 = _service.TGetVisibleList(Mert, true, new PropertyFilter(), 2, 3);
        var huge = _service.TGetVisibleList(Mert, true, new PropertyFilter(), int.MaxValue, 100000);

        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Empty(huge.Items); // taşma yok
    }

    // ---------------- Düzenleme / silme yetkisi ----------------

    [Fact]
    public void AssignedAgent_CanEdit_ButCannotDelete()
    {
        Assert.True(_service.TCanEdit(_mertOnlyMeAssignedToCem, Cem, false));
        Assert.False(_service.TCanDelete(_mertOnlyMeAssignedToCem, Cem, false));
        Assert.Equal(OperationResult.Forbidden, _service.TSoftDelete(_mertOnlyMeAssignedToCem.PropertyId, Cem, false));
    }

    [Fact]
    public void OtherAgent_OnPublic_CannotEditOrDelete()
    {
        Assert.False(_service.TCanEdit(_aysePublic, Bora, false));
        Assert.Equal(OperationResult.Forbidden, _service.TUpdate(_aysePublic.PropertyId, Changes(), Bora, false));
        Assert.Equal(OperationResult.Forbidden, _service.TSoftDelete(_aysePublic.PropertyId, Bora, false));
    }

    [Fact]
    public void OtherAgent_OnHidden_IsNotFound()
    {
        Assert.Equal(OperationResult.NotFound, _service.TUpdate(_boraOnlyMe.PropertyId, Changes(), Ayse, false));
        Assert.Equal(OperationResult.NotFound, _service.TSoftDelete(_boraOnlyMe.PropertyId, Ayse, false));
    }

    [Fact]
    public void Creator_CanDelete_AndRecordIsHiddenButKept()
    {
        Assert.Equal(OperationResult.Success, _service.TSoftDelete(_ayseOnlyMe.PropertyId, Ayse, false));
        Assert.Null(_service.TGetVisibleById(_ayseOnlyMe.PropertyId, Mert, true));
        Assert.True(_context.Properties.Single(p => p.PropertyId == _ayseOnlyMe.PropertyId).IsDeleted);
    }

    // ---------------- Güncelleme kuralları ----------------

    private static Property Changes(string title = "Yeni Başlık", int agent = 0, int? ownerId = null) => new()
    {
        Title = title, ListingType = ListingType.Sale, PropertyType = PropertyType.Residence, Status = PropertyStatus.Sold,
        Price = 4_500_000, City = "İstanbul", District = "Kadıköy", Visibility = Visibility.Public,
        AgentUserId = agent, OwnerContactId = ownerId,
        CreatedByUserId = 999, IsDeleted = true, CreatedAt = new DateTime(2000, 1, 1) // sahte: değişmemeli
    };

    [Fact]
    public void Update_KeepsProtectedFields()
    {
        var createdAt = _ayseOnlyMe.CreatedAt;
        Assert.Equal(OperationResult.Success, _service.TUpdate(_ayseOnlyMe.PropertyId, Changes(), Ayse, false));

        var p = _service.TGetVisibleById(_ayseOnlyMe.PropertyId, Ayse, false)!;
        Assert.Equal("Yeni Başlık", p.Title);
        Assert.Equal(PropertyStatus.Sold, p.Status);
        Assert.Equal(Ayse, p.CreatedByUserId);
        Assert.False(p.IsDeleted);
        Assert.Equal(createdAt, p.CreatedAt);
        Assert.NotNull(p.UpdatedAt);
    }

    [Fact]
    public void Update_AgentCannotReassign_ManagerCan()
    {
        // Cem (sorumlu) sorumluyu Bora yapmaya çalışır -> yok sayılır
        Assert.Equal(OperationResult.Success, _service.TUpdate(_mertOnlyMeAssignedToCem.PropertyId, Changes(agent: Bora), Cem, false));
        Assert.Equal(Cem, _service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Mert, true)!.AgentUserId);

        // Yönetici değiştirebilir
        Assert.Equal(OperationResult.Success, _service.TUpdate(_mertOnlyMeAssignedToCem.PropertyId, Changes(agent: Bora), Mert, true));
        Assert.Equal(Bora, _service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Mert, true)!.AgentUserId);

        // Changes() görünürlüğü Public yapıyor; gizliye geri al ve görünürlüğün sorumluyla birlikte taşındığını doğrula
        var back = Changes(agent: Bora); back.Visibility = Visibility.OnlyMe;
        Assert.Equal(OperationResult.Success, _service.TUpdate(_mertOnlyMeAssignedToCem.PropertyId, back, Mert, true));
        Assert.Null(_service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Cem, false));     // eski sorumlu artık göremez
        Assert.NotNull(_service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Bora, false)); // yeni sorumlu görür
    }

    // ---------------- Oluşturma kuralları ----------------

    [Fact]
    public void Create_Agent_CannotAssignSomeoneElse()
    {
        var p = Add("Danışman İlanı", Ayse, false, Visibility.OnlyMe, agent: Bora);
        Assert.Equal(Ayse, p.AgentUserId);
        Assert.Equal(Ayse, p.CreatedByUserId);
    }

    [Fact]
    public void Create_DefaultsToOnlyMe_AndInvalidEnumsAreNormalized()
    {
        var p = new Property
        {
            Title = "T", City = "C", District = "D", Price = 1,
            Visibility = (Visibility)99, ListingType = (ListingType)99, PropertyType = (PropertyType)99, Status = (PropertyStatus)99
        };
        Assert.Equal(OperationResult.Success, _service.TCreate(p, Ayse, false));
        Assert.Equal(Visibility.OnlyMe, p.Visibility);
        Assert.Equal(ListingType.Sale, p.ListingType);
        Assert.Equal(PropertyType.Other, p.PropertyType);
        Assert.Equal(PropertyStatus.Active, p.Status);
        Assert.Equal(Visibility.OnlyMe, new Property().Visibility);
    }

    [Theory]
    [InlineData(-1, null, null)]       // negatif fiyat
    [InlineData(1, 91.0, null)]        // geçersiz enlem
    [InlineData(1, null, -181.0)]      // geçersiz boylam
    public void Create_InvalidValues_AreRejected(double price, double? lat, double? lng)
    {
        var p = new Property
        {
            Title = "T", City = "C", District = "D", Price = (decimal)price,
            Latitude = (decimal?)lat, Longitude = (decimal?)lng
        };
        Assert.Equal(OperationResult.Invalid, _service.TCreate(p, Ayse, false));
    }

    // ---------------- Mülk sahibi (bağlı müşteri) ----------------

    private Contact AddContact(int owner, Visibility visibility, string phone)
    {
        var c = new Contact { Name = "Sahip", Surname = owner.ToString(), Phone = phone, Visibility = visibility };
        _contacts.TCreate(c, owner);
        return c;
    }

    [Fact]
    public void Create_WithHiddenContactAsOwner_IsInvalid()
    {
        var hidden = AddContact(Bora, Visibility.OnlyMe, "0100 700 00 01");
        var p = new Property { Title = "T", City = "C", District = "D", Price = 1, OwnerContactId = hidden.ContactId };

        Assert.Equal(OperationResult.Invalid, _service.TCreate(p, Ayse, false));
    }

    [Fact]
    public void Create_WithVisibleOwner_Succeeds_AndOwnerVisibleOnlyToThoseWhoSeeTheContact()
    {
        var ownerOfAyse = AddContact(Ayse, Visibility.OnlyMe, "0100 700 00 02");
        var p = Add("Sahipli Daire", Ayse, false, Visibility.Public, ownerId: ownerOfAyse.ContactId);

        // Portföy herkese açık: Bora portföyü görür ama Ayşe'nin gizli müşterisini (sahibi) göremez
        var forBora = _service.TGetVisibleById(p.PropertyId, Bora, false)!;
        Assert.Null(_service.TGetVisibleOwner(forBora, Bora, false));
        Assert.NotNull(_service.TGetVisibleOwner(forBora, Ayse, false));
        Assert.NotNull(_service.TGetVisibleOwner(forBora, Mert, true));
        Assert.Null(forBora.OwnerContact); // detayda sahip nesnesi yüklenmez
    }

    [Fact]
    public void Update_ChangingOwnerToHiddenContact_IsInvalid_ButKeepingExistingIsAllowed()
    {
        var ownerOfMert = AddContact(Mert, Visibility.OnlyMe, "0100 700 00 03");
        // Yönetici, Cem'e atanmış portföye kendi gizli müşterisini sahip yapar
        Assert.Equal(OperationResult.Success,
            _service.TUpdate(_mertOnlyMeAssignedToCem.PropertyId, Changes(ownerId: ownerOfMert.ContactId), Mert, true));

        // Cem (sorumlu) sahibi göremez: formda sahip id'si olmadan (null) düzenler -> sahip KORUNUR, bağlantı kaybolmaz
        Assert.Equal(OperationResult.Success,
            _service.TUpdate(_mertOnlyMeAssignedToCem.PropertyId, Changes("Cem düzenledi", ownerId: null), Cem, false));
        Assert.Equal(ownerOfMert.ContactId, _service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Mert, true)!.OwnerContactId);

        // Cem başka bir id (ör. Bora'nın gizli müşterisi) gönderse de yok sayılır: sonuç aynı (id tahmini yapılamaz)
        var hiddenOfBora = AddContact(Bora, Visibility.OnlyMe, "0100 700 00 04");
        Assert.Equal(OperationResult.Success,
            _service.TUpdate(_mertOnlyMeAssignedToCem.PropertyId, Changes(ownerId: hiddenOfBora.ContactId), Cem, false));
        Assert.Equal(ownerOfMert.ContactId, _service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Mert, true)!.OwnerContactId);
    }

    [Fact]
    public void Update_UserWhoSeesOwner_CannotSetHiddenContactAsOwner()
    {
        // Ayşe kendi portföyünde (sahibi yok) Bora'nın gizli müşterisini sahip yapamaz
        var hiddenOfBora = AddContact(Bora, Visibility.OnlyMe, "0100 700 00 05");
        Assert.Equal(OperationResult.Invalid,
            _service.TUpdate(_ayseOnlyMe.PropertyId, Changes(ownerId: hiddenOfBora.ContactId), Ayse, false));
        Assert.Null(_service.TGetVisibleById(_ayseOnlyMe.PropertyId, Ayse, false)!.OwnerContactId);
    }

    [Fact]
    public void Update_AssignedAgent_CanChangeVisibility_UserDecision()
    {
        // Kullanıcı kararı (görev 11): sorumlu danışman da görünürlüğü değiştirebilir
        var open = Changes(); open.Visibility = Visibility.Public;
        Assert.Equal(OperationResult.Success, _service.TUpdate(_mertOnlyMeAssignedToCem.PropertyId, open, Cem, false));
        Assert.NotNull(_service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Ayse, false)); // artık herkese açık
    }

    [Fact]
    public void Update_InvalidValues_AreRejected_AndNothingChanges()
    {
        var bad = Changes("Bozuk"); bad.Price = -5;
        Assert.Equal(OperationResult.Invalid, _service.TUpdate(_ayseOnlyMe.PropertyId, bad, Ayse, false));
        Assert.Equal("Kadıköy 3+1 Daire", _service.TGetVisibleById(_ayseOnlyMe.PropertyId, Ayse, false)!.Title);
    }

    [Fact]
    public void Update_And_Delete_OnDeletedProperty_AreNotFound()
    {
        Assert.Equal(OperationResult.NotFound, _service.TUpdate(_boraDeletedPublic.PropertyId, Changes(), Mert, true));
        Assert.Equal(OperationResult.NotFound, _service.TSoftDelete(_boraDeletedPublic.PropertyId, Mert, true));
    }

    // ---------------- Sorumlu danışman doğrulaması ----------------

    [Theory]
    [InlineData(999)]     // olmayan kullanıcı
    [InlineData(Pasif)]   // pasif çalışan
    [InlineData(Rolsuz)]  // rolü olmayan kullanıcı
    public void Manager_CannotAssignInvalidAgent(int agentId)
    {
        var p = new Property { Title = "T", City = "C", District = "D", Price = 1, AgentUserId = agentId };
        Assert.Equal(OperationResult.Invalid, _service.TCreate(p, Mert, true));

        Assert.Equal(OperationResult.Invalid,
            _service.TUpdate(_mertOnlyMeAssignedToCem.PropertyId, Changes(agent: agentId), Mert, true));
        Assert.Equal(Cem, _service.TGetVisibleById(_mertOnlyMeAssignedToCem.PropertyId, Mert, true)!.AgentUserId);
    }

    [Fact]
    public void List_DoesNotLoadOwnerContact()
    {
        var owner = AddContact(Ayse, Visibility.OnlyMe, "0100 700 00 06");
        Add("Sahipli", Ayse, false, Visibility.Public, ownerId: owner.ContactId);

        var items = _service.TGetVisibleList(Bora, false, new PropertyFilter(), 1, 100).Items;
        Assert.All(items, p => Assert.Null(p.OwnerContact));
    }
}
