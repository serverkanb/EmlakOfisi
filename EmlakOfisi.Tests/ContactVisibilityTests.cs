using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.Tests;

// CLAUDE.md kural 1-3: görünürlük sunucuda, varsayılan "Sadece Ben", görünürlük ≠ düzenleme.
// Kullanıcılar: Ayşe (danışman, id 1), Bora (danışman, id 2), Mert (yönetici, canSeeAll).
public class ContactVisibilityTests : IDisposable
{
    private const int Ayse = 1;
    private const int Bora = 2;
    private const int Mert = 3;

    private readonly AppDbContext _context;
    private readonly ContactManager _service;

    private readonly Contact _ayseOnlyMe;
    private readonly Contact _aysePublic;
    private readonly Contact _boraOnlyMe;
    private readonly Contact _boraPublic;
    private readonly Contact _boraDeletedPublic;

    public ContactVisibilityTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _service = new ContactManager(new EfContactDal(_context));

        _ayseOnlyMe = Add("Ali", "Gizli", "0532 111 11 11", Ayse, Visibility.OnlyMe, buyer: true);
        _aysePublic = Add("Banu", "Acik", "0532 222 22 22", Ayse, Visibility.Public, seller: true);
        _boraOnlyMe = Add("Cem", "Saklı", "0533 333 33 33", Bora, Visibility.OnlyMe, buyer: true, landlord: true);
        _boraPublic = Add("Deniz", "Herkes", "0533 444 44 44", Bora, Visibility.Public, tenant: true);
        _boraDeletedPublic = Add("Ece", "Silindi", "0533 555 55 55", Bora, Visibility.Public);
        _service.TSoftDelete(_boraDeletedPublic.ContactId, Bora, canSeeAll: false);
    }

    private Contact Add(string name, string surname, string phone, int owner, Visibility visibility,
        bool buyer = false, bool seller = false, bool tenant = false, bool landlord = false)
    {
        var contact = new Contact
        {
            Name = name, Surname = surname, Phone = phone, Visibility = visibility,
            IsBuyer = buyer, IsSeller = seller, IsTenant = tenant, IsLandlord = landlord
        };
        _service.TCreate(contact, owner);
        return contact;
    }

    private List<string> VisibleNames(int userId, bool canSeeAll, string? search = null, ContactRole? role = null) =>
        _service.TGetVisibleList(userId, canSeeAll, search, role, 1, 100).Items.Select(c => c.Name).OrderBy(n => n).ToList();

    public void Dispose() => _context.Dispose();

    // ---------- Liste: 3 rol × 2 görünürlük ----------

    [Fact]
    public void Agent_SeesOwnRecords_AndPublicRecords_Only()
    {
        Assert.Equal(new[] { "Ali", "Banu", "Deniz" }, VisibleNames(Ayse, canSeeAll: false));
    }

    [Fact]
    public void OtherAgent_DoesNotSeeFirstAgentsOnlyMe()
    {
        Assert.Equal(new[] { "Banu", "Cem", "Deniz" }, VisibleNames(Bora, canSeeAll: false));
    }

    [Fact]
    public void Manager_SeesAllNonDeletedRecords()
    {
        Assert.Equal(new[] { "Ali", "Banu", "Cem", "Deniz" }, VisibleNames(Mert, canSeeAll: true));
    }

    [Fact]
    public void UserWithNoRecords_SeesOnlyPublic()
    {
        Assert.Equal(new[] { "Banu", "Deniz" }, VisibleNames(99, canSeeAll: false));
    }

    [Fact]
    public void DeletedRecords_AreHiddenFromEveryone()
    {
        Assert.DoesNotContain("Ece", VisibleNames(Bora, canSeeAll: false));
        Assert.DoesNotContain("Ece", VisibleNames(Mert, canSeeAll: true));
    }

    // ---------- Detay (doğrudan id ile) ----------

    [Fact]
    public void GetById_OthersOnlyMe_ReturnsNull()
    {
        Assert.Null(_service.TGetVisibleById(_boraOnlyMe.ContactId, Ayse, canSeeAll: false));
    }

    [Fact]
    public void GetById_OthersPublic_ReturnsRecord()
    {
        Assert.NotNull(_service.TGetVisibleById(_boraPublic.ContactId, Ayse, canSeeAll: false));
    }

    [Fact]
    public void GetById_OwnOnlyMe_ReturnsRecord()
    {
        Assert.NotNull(_service.TGetVisibleById(_ayseOnlyMe.ContactId, Ayse, canSeeAll: false));
    }

    [Fact]
    public void GetById_Manager_SeesOthersOnlyMe()
    {
        Assert.NotNull(_service.TGetVisibleById(_boraOnlyMe.ContactId, Mert, canSeeAll: true));
    }

    [Fact]
    public void GetById_Deleted_ReturnsNull_EvenForManager()
    {
        Assert.Null(_service.TGetVisibleById(_boraDeletedPublic.ContactId, Mert, canSeeAll: true));
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(_service.TGetVisibleById(9999, Mert, canSeeAll: true));
    }

    // ---------- Arama gizli kaydı sızdırmamalı ----------

    [Theory]
    [InlineData("Cem")]          // ad
    [InlineData("Saklı")]        // soyad
    [InlineData("Cem Saklı")]    // ad soyad
    [InlineData("Saklı Cem")]    // soyad ad
    [InlineData("0533 333 33 33")] // telefon
    [InlineData("333 33")]       // kısmi telefon
    public void Search_DoesNotLeakOthersOnlyMe(string search)
    {
        Assert.Empty(VisibleNames(Ayse, canSeeAll: false, search));
    }

    [Theory]
    [InlineData("Cem")]
    [InlineData("0533 333 33 33")]
    [InlineData("+90 533 333 3333")]
    public void Search_OwnerFindsOwnOnlyMe(string search)
    {
        Assert.Equal(new[] { "Cem" }, VisibleNames(Bora, canSeeAll: false, search));
    }

    // Not: büyük/küçük harf duyarsızlığı SQL Server collation'ından gelir; InMemory veritabanı
    // harfe duyarlı olduğu için burada test edilemez (elle LocalDB'de denenir).
    [Theory]
    [InlineData("Banu")]
    [InlineData("Banu Acik")]
    [InlineData("Acik Banu")]   // soyad ad sırası
    [InlineData("Aci")]         // kısmi soyad
    [InlineData("05322222222")]
    [InlineData("905322222222")]
    [InlineData("2222")]
    public void Search_ByNameOrPhoneFormats_FindsPublicRecord(string search)
    {
        Assert.Equal(new[] { "Banu" }, VisibleNames(Bora, canSeeAll: false, search));
    }

    // ---------- Rol filtresi ----------

    [Fact]
    public void RoleFilter_MultiRoleContact_AppearsInEachRole()
    {
        Assert.Contains("Cem", VisibleNames(Mert, true, role: ContactRole.Buyer));
        Assert.Contains("Cem", VisibleNames(Mert, true, role: ContactRole.Landlord));
        Assert.DoesNotContain("Cem", VisibleNames(Mert, true, role: ContactRole.Tenant));
    }

    [Fact]
    public void RoleFilter_StillAppliesVisibility()
    {
        // Alıcılar: Ali (Ayşe, gizli) ve Cem (Bora, gizli). Ayşe sadece kendi kaydını görmeli.
        Assert.Equal(new[] { "Ali" }, VisibleNames(Ayse, false, role: ContactRole.Buyer));
    }

    // ---------- Sayfalama ----------

    [Fact]
    public void Paging_PageSizeIsCapped()
    {
        // Çok büyük sayfa boyutu 100 ile sınırlanır (burada 4 kayıt var, hepsi gelir; hata olmaz)
        var result = _service.TGetVisibleList(Mert, true, null, null, page: 1, pageSize: 100000);
        Assert.Equal(4, result.Items.Count);
    }

    [Fact]
    public void Paging_ReturnsPageAndTotal()
    {
        var page1 = _service.TGetVisibleList(Mert, true, null, null, page: 1, pageSize: 3);
        var page2 = _service.TGetVisibleList(Mert, true, null, null, page: 2, pageSize: 3);

        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Equal("Deniz", page2.Items[0].Name);
    }

    // ---------- Düzenleme / silme (görünürlükten ayrı) ----------

    [Fact]
    public void CanEdit_OthersPublic_IsFalse_EvenThoughVisible()
    {
        Assert.NotNull(_service.TGetVisibleById(_boraPublic.ContactId, Ayse, false)); // görüyor
        Assert.False(_service.TCanEdit(_boraPublic, Ayse, false));                  // ama düzenleyemez
        Assert.False(_service.TCanDelete(_boraPublic, Ayse, false));                // silemez
    }

    [Fact]
    public void CanEdit_Own_IsTrue()
    {
        Assert.True(_service.TCanEdit(_ayseOnlyMe, Ayse, false));
        Assert.True(_service.TCanDelete(_ayseOnlyMe, Ayse, false));
    }

    [Fact]
    public void CanEdit_Manager_IsTrue_ForOthersRecords()
    {
        Assert.True(_service.TCanEdit(_boraOnlyMe, Mert, true));
        Assert.True(_service.TCanDelete(_boraOnlyMe, Mert, true));
    }

    [Fact]
    public void CanEdit_DeletedRecord_IsFalse()
    {
        Assert.False(_service.TCanEdit(_boraDeletedPublic, Mert, true));
        Assert.False(_service.TCanDelete(_boraDeletedPublic, Mert, true));
    }

    [Fact]
    public void CanEdit_OthersOnlyMe_IsFalse_ForAgent()
    {
        Assert.False(_service.TCanEdit(_boraOnlyMe, Ayse, false));
        Assert.False(_service.TCanDelete(_boraOnlyMe, Ayse, false));
    }

    // ---------- Güncelleme: yetki serviste kontrol edilir ----------

    private static Contact Changes(string name = "Yeni", Visibility visibility = Visibility.Public) => new()
    {
        Name = name, Surname = "Ad", Phone = "0555 000 00 01", Visibility = visibility,
        CreatedByUserId = 777, IsDeleted = true, CreatedAt = new DateTime(2000, 1, 1) // sahte değerler: değişmemeli
    };

    [Fact]
    public void Update_Own_Succeeds_AndProtectedFieldsAreKept()
    {
        var createdAt = _ayseOnlyMe.CreatedAt;

        var result = _service.TUpdate(_ayseOnlyMe.ContactId, Changes("Alihan"), Ayse, false);

        Assert.Equal(OperationResult.Success, result);
        var reloaded = _service.TGetVisibleById(_ayseOnlyMe.ContactId, Ayse, false)!;
        Assert.Equal("Alihan", reloaded.Name);
        Assert.Equal("5550000001", reloaded.PhoneNormalized);
        Assert.Equal(Visibility.Public, reloaded.Visibility);
        Assert.Equal(Ayse, reloaded.CreatedByUserId);   // sahip değişmedi
        Assert.False(reloaded.IsDeleted);                // silinmedi
        Assert.Equal(createdAt, reloaded.CreatedAt);     // oluşturma tarihi değişmedi
        Assert.NotNull(reloaded.UpdatedAt);
    }

    [Fact]
    public void Update_OthersPublic_IsForbidden_AndNothingChanges()
    {
        var result = _service.TUpdate(_boraPublic.ContactId, Changes("Hack"), Ayse, false);

        Assert.Equal(OperationResult.Forbidden, result);
        Assert.Equal("Deniz", _service.TGetVisibleById(_boraPublic.ContactId, Bora, false)!.Name);
    }

    [Fact]
    public void Update_OthersOnlyMe_IsNotFound()
    {
        Assert.Equal(OperationResult.NotFound, _service.TUpdate(_boraOnlyMe.ContactId, Changes(), Ayse, false));
        Assert.Equal("Cem", _service.TGetVisibleById(_boraOnlyMe.ContactId, Bora, false)!.Name);
    }

    [Fact]
    public void Update_DeletedRecord_IsNotFound()
    {
        Assert.Equal(OperationResult.NotFound, _service.TUpdate(_boraDeletedPublic.ContactId, Changes(), Mert, true));
    }

    [Fact]
    public void Update_Manager_CanEditOthersOnlyMe()
    {
        Assert.Equal(OperationResult.Success, _service.TUpdate(_boraOnlyMe.ContactId, Changes("Cemal", Visibility.OnlyMe), Mert, true));
        Assert.Equal(Bora, _service.TGetVisibleById(_boraOnlyMe.ContactId, Bora, false)!.CreatedByUserId);
    }

    [Fact]
    public void Update_InvalidVisibilityValue_KeepsOldVisibility()
    {
        _service.TUpdate(_ayseOnlyMe.ContactId, Changes(visibility: (Visibility)99), Ayse, false);
        Assert.Equal(Visibility.OnlyMe, _service.TGetVisibleById(_ayseOnlyMe.ContactId, Ayse, false)!.Visibility);
    }

    // ---------- Silme: yetki serviste kontrol edilir ----------

    [Fact]
    public void SoftDelete_OthersPublic_IsForbidden()
    {
        Assert.Equal(OperationResult.Forbidden, _service.TSoftDelete(_boraPublic.ContactId, Ayse, false));
        Assert.NotNull(_service.TGetVisibleById(_boraPublic.ContactId, Ayse, false));
    }

    [Fact]
    public void SoftDelete_OthersOnlyMe_IsNotFound()
    {
        Assert.Equal(OperationResult.NotFound, _service.TSoftDelete(_boraOnlyMe.ContactId, Ayse, false));
    }

    // ---------- Oluşturma ----------

    [Fact]
    public void Create_SetsOwner_DefaultOnlyMe_AndNormalizedPhone()
    {
        var contact = new Contact { Name = "Fatma", Surname = "Yeni", Phone = "+90 (544) 123-45-67" };
        contact.CreatedByUserId = 777; // formdan sahte değer gelse bile
        contact.IsDeleted = true;
        _service.TCreate(contact, Ayse);
        Assert.False(contact.IsDeleted);

        Assert.Equal(Ayse, contact.CreatedByUserId);
        Assert.Equal(Visibility.OnlyMe, contact.Visibility);
        Assert.Equal("5441234567", contact.PhoneNormalized);
        Assert.Null(_service.TGetVisibleById(contact.ContactId, Bora, false));
    }

    [Fact]
    public void Create_InvalidVisibilityValue_FallsBackToOnlyMe()
    {
        var contact = new Contact { Name = "G", Surname = "H", Phone = "0544 000 00 00", Visibility = (Visibility)99 };
        _service.TCreate(contact, Ayse);
        Assert.Equal(Visibility.OnlyMe, contact.Visibility);
    }

    [Fact]
    public void SoftDelete_HidesRecord_ButKeepsRow()
    {
        Assert.Equal(OperationResult.Success, _service.TSoftDelete(_aysePublic.ContactId, Ayse, false));

        Assert.Null(_service.TGetVisibleById(_aysePublic.ContactId, Ayse, false));
        Assert.True(_context.Contacts.Any(c => c.ContactId == _aysePublic.ContactId && c.IsDeleted));
    }

    // ---------- Tekrar kayıt kontrolü: gizli kaydın kimliği sızmamalı ----------

    [Fact]
    public void DuplicatePhone_OthersOnlyMe_ExistsButIdIsHidden()
    {
        var result = _service.TCheckDuplicatePhone("05333333333", Ayse, false);

        Assert.True(result.Exists);
        Assert.Null(result.VisibleContactId);
    }

    [Fact]
    public void DuplicatePhone_VisibleRecord_ReturnsId()
    {
        Assert.Equal(_boraPublic.ContactId, _service.TCheckDuplicatePhone("0533 444 44 44", Ayse, false).VisibleContactId);
        Assert.Equal(_boraOnlyMe.ContactId, _service.TCheckDuplicatePhone("+90 533 333 3333", Mert, true).VisibleContactId);
    }

    [Fact]
    public void DuplicatePhone_DeletedOrUnknown_DoesNotExist()
    {
        Assert.False(_service.TCheckDuplicatePhone("0533 555 55 55", Mert, true).Exists); // silinmiş
        Assert.False(_service.TCheckDuplicatePhone("0500 000 00 00", Mert, true).Exists);
        Assert.False(_service.TCheckDuplicatePhone("", Mert, true).Exists);
    }

    [Fact]
    public void DuplicatePhone_PrefersVisibleRecord_WhenHiddenOneHasLowerId()
    {
        // Aynı numara: önce Bora'nın gizli kaydı (küçük id), sonra Bora'nın herkese açık kaydı.
        Add("Gizli2", "Kayit", "0100 777 77 77", Bora, Visibility.OnlyMe);
        var open = Add("Acik2", "Kayit", "0100 777 77 77", Bora, Visibility.Public);

        var result = _service.TCheckDuplicatePhone("0100 777 77 77", Ayse, false);

        Assert.True(result.Exists);
        Assert.Equal(open.ContactId, result.VisibleContactId);
    }

    [Fact]
    public void DuplicatePhone_ExcludesOwnRecordOnUpdate()
    {
        Assert.False(_service.TCheckDuplicatePhone("0532 111 11 11", Ayse, false, excludeContactId: _ayseOnlyMe.ContactId).Exists);
    }
}
