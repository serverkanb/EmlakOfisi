using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Helpers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Görev 22: kira tarihleri ve kontrat dosyası. Ayşe, Bora: danışman. Mert: yönetici.
// Dosyayı işlemi gören açar; yükleme/kaldırma işlemi düzenleyebilene (aynı gün kuralı dahil).
public class ContractTests
{
    private static readonly byte[] Pdf = "%PDF-1.4\n%test"u8.ToArray();
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1 };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D };
    private static readonly byte[] Html = "<html><script>alert(1)</script></html>"u8.ToArray();

    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public ContactManager Contacts { get; private init; } = null!;
        public PropertyManager Properties { get; private init; } = null!;
        public TransactionManager Transactions { get; private init; } = null!;
        public DocumentFileStore Store { get; private init; } = null!;
        public string Root { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
        public AppUser Mert { get; private init; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var contacts = new ContactManager(new EfContactDal(identity.Db));
            var properties = new PropertyManager(new EfPropertyDal(identity.Db), contacts);
            var root = Path.Combine(Path.GetTempPath(), "emlak-test-docs-" + Guid.NewGuid().ToString("N"));
            return new Fixture
            {
                Identity = identity,
                Contacts = contacts,
                Properties = properties,
                Transactions = TestControllers.Transactions(identity.Db, contacts, properties),
                Store = new DocumentFileStore(root),
                Root = root,
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        public bool SeesAll(AppUser u) => u == Mert;

        public TransactionController As(AppUser u) =>
            Identity.SignedInAs(TestControllers.Transaction(Identity.Db, Contacts, Properties, Store), u,
                u == Mert ? RoleNames.Manager : RoleNames.Agent);

        public Transaction Add(AppUser creator, ListingType listing = ListingType.Rent,
            DateTime? start = null, DateTime? end = null, TransactionStatus status = TransactionStatus.Offer)
        {
            var c = new Contact { Name = "Kiracı", Surname = "Test", Phone = "01000000001", Visibility = Visibility.Public, IsTenant = true, IsBuyer = true };
            Contacts.TCreate(c, Mert.Id);
            var p = new Property { Title = "Daire", City = "İzmir", District = "Bornova", Price = 1, Visibility = Visibility.Public, ListingType = listing };
            Assert.Equal(OperationResult.Success, Properties.TCreate(p, Mert.Id, true));

            var t = new Transaction
            {
                PropertyId = p.PropertyId, CustomerContactId = c.ContactId, Status = status,
                LeaseStartDate = start, LeaseEndDate = end
            };
            Assert.Equal(OperationResult.Success, Transactions.TCreate(t, creator.Id, SeesAll(creator), out _));
            return t;
        }

        public Transaction Read(int id) => Transactions.TGetVisibleById(id, Mert.Id, true)!.Transaction;

        public static IFormFile File(byte[] content, string name = "kontrat.pdf") =>
            new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

        public string[] FilesOnDisk() =>
            Directory.Exists(Root) ? Directory.GetFiles(Root, "*", SearchOption.AllDirectories) : Array.Empty<string>();

        public void Dispose()
        {
            Identity.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    // ---------------- Dosya tipi ----------------

    [Fact]
    public void Validator_DetectsFromContent()
    {
        Assert.Equal(("application/pdf", ".pdf"), ContractFileValidator.Detect(Pdf));
        Assert.Equal(("image/jpeg", ".jpg"), ContractFileValidator.Detect(Jpeg));
        Assert.Equal("image/png", ContractFileValidator.Detect(Png)!.Value.ContentType);
        Assert.Null(ContractFileValidator.Detect(Html));
        Assert.Null(ContractFileValidator.Detect("RIFF\0\0\0\0WEBP"u8));   // WebP kontrat için kabul edilmez
        Assert.Null(ContractFileValidator.Detect(ReadOnlySpan<byte>.Empty));
    }

    // ---------------- Kira tarihleri ----------------

    [Fact]
    public async Task LeaseDates_SavedForRent_ClearedForSale()
    {
        using var f = await Fixture.CreateAsync();
        var rent = f.Add(f.Ayse, ListingType.Rent, new DateTime(2026, 1, 1, 15, 30, 0), new DateTime(2027, 1, 1));
        var sale = f.Add(f.Ayse, ListingType.Sale, new DateTime(2026, 1, 1), new DateTime(2027, 1, 1));

        Assert.Equal(new DateTime(2026, 1, 1), f.Read(rent.TransactionId).LeaseStartDate);   // saat atılır
        Assert.Equal(new DateTime(2027, 1, 1), f.Read(rent.TransactionId).LeaseEndDate);
        Assert.Null(f.Read(sale.TransactionId).LeaseStartDate);
        Assert.Null(f.Read(sale.TransactionId).LeaseEndDate);
    }

    [Fact]
    public async Task LeaseEnd_BeforeStart_Invalid_InService()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        var result = f.Transactions.TUpdate(t.TransactionId, new Transaction
        {
            CustomerContactId = t.CustomerContactId, Status = TransactionStatus.Offer,
            LeaseStartDate = new DateTime(2026, 5, 1), LeaseEndDate = new DateTime(2026, 5, 1)
        }, f.Ayse.Id, false, out _);
        Assert.Equal(OperationResult.Invalid, result);
    }

    [Fact]
    public async Task Controller_LeaseEndBeforeStart_FormError_NothingSaved()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        var result = f.As(f.Ayse).Edit(t.TransactionId, new TransactionFormViewModel
        {
            CustomerContactId = t.CustomerContactId, Status = TransactionStatus.Offer,
            LeaseStartDate = new DateTime(2026, 6, 1), LeaseEndDate = new DateTime(2026, 5, 1)
        });
        Assert.IsType<ViewResult>(result);
        Assert.Null(f.Read(t.TransactionId).LeaseEndDate);
    }

    [Fact]
    public async Task Controller_EditRent_SavesLeaseDates_AndCardShowsThem()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        Assert.IsType<RedirectToActionResult>(f.As(f.Ayse).Edit(t.TransactionId, new TransactionFormViewModel
        {
            CustomerContactId = t.CustomerContactId, Status = TransactionStatus.Agreed,
            LeaseStartDate = new DateTime(2026, 6, 1), LeaseEndDate = new DateTime(2027, 6, 1)
        }));

        var list = Assert.IsType<TransactionListViewModel>(Assert.IsType<ViewResult>(f.As(f.Ayse).Index()).Model);
        var item = Assert.Single(list.Items);
        Assert.Equal(new DateTime(2027, 6, 1), item.LeaseEndDate);
        Assert.False(item.HasContract);
    }

    // ---------------- Yükleme ----------------

    [Fact]
    public async Task Upload_ByEditor_Saved_ReplaceDeletesOld()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);

        Assert.IsType<RedirectToActionResult>(await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Pdf, @"C:\belgeler\Kira Sözleşmesi.pdf")));
        var saved = f.Read(t.TransactionId);
        Assert.Equal("application/pdf", saved.ContractContentType);
        Assert.Equal("Kira Sözleşmesi.pdf", saved.ContractOriginalName);   // yol kısmı atılır
        Assert.StartsWith($"{t.TransactionId}/", saved.ContractFileName);
        Assert.Equal(f.Ayse.Id, saved.ContractUploadedByUserId);
        var first = Assert.Single(f.FilesOnDisk());

        // Değiştirme: yeni dosya, eskisi diskten silinir
        Assert.IsType<RedirectToActionResult>(await f.As(f.Mert).UploadContract(t.TransactionId, Fixture.File(Jpeg, "foto.jpg")));
        var replaced = Assert.Single(f.FilesOnDisk());
        Assert.NotEqual(first, replaced);
        Assert.Equal("image/jpeg", f.Read(t.TransactionId).ContractContentType);
    }

    [Fact]
    public async Task Upload_FakePdf_Rejected_NothingWritten()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        var c = f.As(f.Ayse);
        Assert.IsType<RedirectToActionResult>(await c.UploadContract(t.TransactionId, Fixture.File(Html, "kontrat.pdf")));
        Assert.Equal("Sadece PDF, JPG ya da PNG dosyası yüklenebilir.", c.TempData["Error"]);
        Assert.Null(f.Read(t.TransactionId).ContractFileName);
        Assert.Empty(f.FilesOnDisk());
    }

    [Fact]
    public async Task Upload_TooLargeOrEmpty_Rejected()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        var big = new byte[ContractFileValidator.MaxBytes + 1];
        Pdf.CopyTo(big, 0);
        Assert.IsType<RedirectToActionResult>(await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(big)));
        Assert.IsType<RedirectToActionResult>(await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Array.Empty<byte>())));
        Assert.IsType<RedirectToActionResult>(await f.As(f.Ayse).UploadContract(t.TransactionId, null));
        Assert.Null(f.Read(t.TransactionId).ContractFileName);
        Assert.Empty(f.FilesOnDisk());
    }

    [Fact]
    public async Task Upload_NotVisible404_BeforeFileCheck_NothingWritten()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);   // Bora işlemi görmez (komisyon)
        Assert.IsType<NotFoundResult>(await f.As(f.Bora).UploadContract(t.TransactionId, Fixture.File(Pdf)));
        Assert.IsType<NotFoundResult>(await f.As(f.Bora).UploadContract(t.TransactionId, Fixture.File(Html)));
        Assert.IsType<NotFoundResult>(f.As(f.Bora).RemoveContract(t.TransactionId));
        Assert.IsType<NotFoundResult>(f.As(f.Bora).Contract(t.TransactionId));
        Assert.IsType<NotFoundResult>(await f.As(f.Ayse).UploadContract(99_999, Fixture.File(Pdf)));
        Assert.Empty(f.FilesOnDisk());
    }

    [Fact]
    public async Task ClosedYesterday_AgentForbidden_ManagerAllowed()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse, status: TransactionStatus.Completed);
        var tracked = f.Identity.Db.Transactions.Single(x => x.TransactionId == t.TransactionId);
        tracked.ClosedAt = DateTime.Today.AddDays(-1);
        f.Identity.Db.SaveChanges();

        Assert.IsType<ForbidResult>(await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Pdf)));
        Assert.Empty(f.FilesOnDisk());
        Assert.IsType<RedirectToActionResult>(await f.As(f.Mert).UploadContract(t.TransactionId, Fixture.File(Pdf)));
        Assert.IsType<ForbidResult>(f.As(f.Ayse).RemoveContract(t.TransactionId));
        Assert.NotNull(f.Read(t.TransactionId).ContractFileName);

        // Danışman görmeye devam eder (dosyayı açabilir)
        Assert.IsType<PhysicalFileResult>(f.As(f.Ayse).Contract(t.TransactionId));
    }

    // ---------------- Gösterim ve kaldırma ----------------

    [Fact]
    public async Task Contract_Served_WithSafeHeaders()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Pdf, "Sözleşme.pdf"));

        var c = f.As(f.Mert);
        var file = Assert.IsType<PhysicalFileResult>(c.Contract(t.TransactionId));
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("private, no-cache", c.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", c.Response.Headers.XContentTypeOptions.ToString());
        Assert.Empty(c.Response.Headers.ContentSecurityPolicy.ToString());   // PDF görüntüleyici sandbox'ta açılmıyor
        Assert.StartsWith("inline", c.Response.Headers.ContentDisposition.ToString());

        // Resimde CSP sandbox var
        await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Png, "tarama.png"));
        var c2 = f.As(f.Mert);
        Assert.Equal("image/png", Assert.IsType<PhysicalFileResult>(c2.Contract(t.TransactionId)).ContentType);
        Assert.Contains("sandbox", c2.Response.Headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public async Task OriginalName_ExtensionFollowsDetectedType()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        // PDF imzalı ama ".html" adlı dosya: indirildiğinde .html olarak kaydedilip açılmasın
        await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Pdf, "kontrat.html"));
        Assert.Equal("kontrat.pdf", f.Read(t.TransactionId).ContractOriginalName);

        await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Png, "../..\\gizli\\.png"));
        Assert.Equal("kontrat.png", f.Read(t.TransactionId).ContractOriginalName);
    }

    [Fact]
    public async Task CreatorAfterReassign_StillOpensContract_OtherAgentDoesNot()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Pdf));

        // Yönetici sorumluyu Bora'ya verir; Ayşe oluşturan olarak görmeye devam eder
        var tracked = f.Identity.Db.Transactions.Single(x => x.TransactionId == t.TransactionId);
        tracked.AgentUserId = f.Bora.Id;
        f.Identity.Db.SaveChanges();

        Assert.IsType<PhysicalFileResult>(f.As(f.Ayse).Contract(t.TransactionId));
        Assert.IsType<PhysicalFileResult>(f.As(f.Bora).Contract(t.TransactionId));
    }

    [Fact]
    public async Task OtherAgent_OnPublicRecords_CannotOpen()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);   // portföy ve müşteri herkese açık
        await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Pdf));
        Assert.IsType<NotFoundResult>(f.As(f.Bora).Contract(t.TransactionId));
    }

    [Theory]
    [InlineData(@"C:\site\wwwroot", @"C:\site\wwwroot\docs", true)]
    [InlineData(@"C:\site\wwwroot", @"C:\site\wwwroot", true)]
    [InlineData(@"C:\site\wwwroot", @"C:\site\WWWROOT\docs\", true)]
    [InlineData(@"C:\site\wwwroot", @"C:\site\App_Data\documents", false)]
    [InlineData(@"C:\site\wwwroot", @"C:\site\wwwroot2\docs", false)]
    [InlineData(null, @"C:\site\App_Data\documents", false)]
    public void StoreRoot_UnderWebRoot_Detected(string? webRoot, string root, bool inside)
    {
        Assert.Equal(inside, PhotoFileStore.IsInside(root, webRoot));
    }

    [Fact]
    public async Task Remove_WhenNoContract_ShowsError()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        var c = f.As(f.Ayse);
        Assert.IsType<RedirectToActionResult>(c.RemoveContract(t.TransactionId));
        Assert.Equal("Bu işlemde kontrat dosyası yok.", c.TempData["Error"]);
    }

    [Fact]
    public async Task Contract_NoFile_404()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Contract(t.TransactionId));
    }

    [Fact]
    public async Task Remove_ClearsFields_AndDeletesFile()
    {
        using var f = await Fixture.CreateAsync();
        var t = f.Add(f.Ayse);
        await f.As(f.Ayse).UploadContract(t.TransactionId, Fixture.File(Png, "tarama.png"));
        Assert.Single(f.FilesOnDisk());

        Assert.IsType<RedirectToActionResult>(f.As(f.Ayse).RemoveContract(t.TransactionId));
        var saved = f.Read(t.TransactionId);
        Assert.Null(saved.ContractFileName);
        Assert.Null(saved.ContractOriginalName);
        Assert.Null(saved.ContractUploadedByUserId);
        Assert.Empty(f.FilesOnDisk());
        Assert.IsType<NotFoundResult>(f.As(f.Ayse).Contract(t.TransactionId));
    }

    [Fact]
    public async Task Create_IgnoresPostedContractFields()
    {
        using var f = await Fixture.CreateAsync();
        var c = new Contact { Name = "K", Surname = "T", Phone = "01000000009", Visibility = Visibility.Public, IsTenant = true };
        f.Contacts.TCreate(c, f.Mert.Id);
        var p = new Property { Title = "D", City = "İzmir", District = "Bornova", Price = 1, Visibility = Visibility.Public, ListingType = ListingType.Rent };
        f.Properties.TCreate(p, f.Mert.Id, true);

        var t = new Transaction
        {
            PropertyId = p.PropertyId, CustomerContactId = c.ContactId,
            ContractFileName = "../../baskasi.pdf", ContractContentType = "text/html", ContractUploadedByUserId = f.Mert.Id
        };
        Assert.Equal(OperationResult.Success, f.Transactions.TCreate(t, f.Ayse.Id, false, out _));
        var saved = f.Read(t.TransactionId);
        Assert.Null(saved.ContractFileName);
        Assert.Null(saved.ContractContentType);
        Assert.Null(saved.ContractUploadedByUserId);
    }
}
