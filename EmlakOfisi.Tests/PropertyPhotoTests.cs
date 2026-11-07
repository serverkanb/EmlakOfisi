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
using Microsoft.Extensions.Logging.Abstractions;

namespace EmlakOfisi.Tests;

// Görev 13: portföy fotoğrafları. Gerçek Identity + InMemory; dosyalar her test için ayrı geçici klasörde.
// Ayşe, Bora: danışman. Mert: yönetici.
public class PropertyPhotoTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 3 };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
    private static readonly byte[] Webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();
    private static readonly byte[] Html = "<html><script>alert(1)</script></html>"u8.ToArray();

    private sealed class Fixture : IDisposable
    {
        public TestIdentity Identity { get; private init; } = null!;
        public PropertyManager Properties { get; private init; } = null!;
        public PropertyPhotoManager Photos { get; private init; } = null!;
        public PhotoFileStore Store { get; private init; } = null!;
        public string Root { get; private init; } = null!;
        public AppUser Ayse { get; private init; } = null!;
        public AppUser Bora { get; private init; } = null!;
        public AppUser Mert { get; private init; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var identity = await TestIdentity.CreateAsync();
            var contacts = new ContactManager(new EfContactDal(identity.Db));
            var properties = new PropertyManager(new EfPropertyDal(identity.Db), contacts);
            var root = Path.Combine(Path.GetTempPath(), "emlak-test-" + Guid.NewGuid().ToString("N"));
            return new Fixture
            {
                Identity = identity,
                Properties = properties,
                Photos = TestControllers.Photos(identity.Db, properties),
                Store = new PhotoFileStore(root),
                Root = root,
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        public bool SeesAll(AppUser u) => u == Mert;

        public Property Property(AppUser creator, Visibility visibility, int agent = 0)
        {
            var p = new Property { Title = "Portföy", City = "İstanbul", District = "Kadıköy", Price = 1, Visibility = visibility, AgentUserId = agent };
            Assert.Equal(OperationResult.Success, Properties.TCreate(p, creator.Id, SeesAll(creator)));
            return p;
        }

        // Servis üzerinden kayıt (dosya gerektirmeyen testler için)
        public PropertyPhoto AddRow(AppUser u, Property p)
        {
            Assert.Equal(OperationResult.Success, Photos.TAdd(p.PropertyId, $"{p.PropertyId}/x{Guid.NewGuid():N}.jpg", "image/jpeg", "a.jpg", u.Id, SeesAll(u)));
            return Identity.Db.PropertyPhotos.OrderByDescending(x => x.PropertyPhotoId).First();
        }

        public PropertyPhotoController Controller(AppUser u) =>
            Identity.SignedInAs(new PropertyPhotoController(Photos, Store, NullLogger<PropertyPhotoController>.Instance),
                u, SeesAll(u) ? RoleNames.Manager : RoleNames.Agent);

        public static IFormFile File(byte[] content, string name = "foto.jpg") =>
            new FormFile(new MemoryStream(content), 0, content.Length, "files", name);

        public void Dispose()
        {
            Identity.Dispose();
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    // ---------------- Dosya tipi ve saklama ----------------

    [Fact]
    public void Validator_DetectsByContent_NotByName()
    {
        Assert.Equal("image/jpeg", ImageFileValidator.Detect(Jpeg)!.Value.ContentType);
        Assert.Equal("image/png", ImageFileValidator.Detect(Png)!.Value.ContentType);
        Assert.Equal("image/webp", ImageFileValidator.Detect(Webp)!.Value.ContentType);
        Assert.Null(ImageFileValidator.Detect(Html));
        Assert.Null(ImageFileValidator.Detect(ReadOnlySpan<byte>.Empty));
        Assert.Null(ImageFileValidator.Detect("GIF89a......"u8));
    }

    [Fact]
    public async Task FileStore_SavesUnderRoot_AndBlocksPathTraversal()
    {
        using var f = await Fixture.CreateAsync();

        var name = await f.Store.SaveAsync(7, new MemoryStream(Jpeg), ".jpg");

        Assert.StartsWith("7/", name);
        Assert.EndsWith(".jpg", name);
        Assert.NotNull(f.Store.GetPath(name));
        Assert.Null(f.Store.GetPath("../../Windows/win.ini"));
        Assert.Null(f.Store.GetPath(@"..\secret.txt"));

        f.Store.Delete(name);
        Assert.Null(f.Store.GetPath(name));
    }

    [Fact]
    public async Task FileStore_RootWithTrailingSeparator_Works()
    {
        var root = Path.Combine(Path.GetTempPath(), "emlak-test-" + Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
        try
        {
            var store = new PhotoFileStore(root); // ayar "D:\uploads\" gibi girilmiş olabilir
            var name = await store.SaveAsync(3, new MemoryStream(Jpeg), ".jpg");
            Assert.NotNull(store.GetPath(name));
            Assert.Null(store.GetPath("../disari.jpg"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // ---------------- Yetki ----------------

    [Fact]
    public async Task UploadCheck_Matrix()
    {
        using var f = await Fixture.CreateAsync();
        var aysePublic = f.Property(f.Ayse, Visibility.Public);
        var ayseHidden = f.Property(f.Ayse, Visibility.OnlyMe);
        var mertAssignedToBora = f.Property(f.Mert, Visibility.OnlyMe, agent: f.Bora.Id);

        OperationResult Check(AppUser u, Property p) => f.Photos.TCheckUpload(p.PropertyId, u.Id, f.SeesAll(u), out _);

        Assert.Equal(OperationResult.Success, Check(f.Ayse, aysePublic));          // oluşturan
        Assert.Equal(OperationResult.Forbidden, Check(f.Bora, aysePublic));        // görüyor ama düzenleyemiyor
        Assert.Equal(OperationResult.NotFound, Check(f.Bora, ayseHidden));         // göremiyor
        Assert.Equal(OperationResult.Success, Check(f.Bora, mertAssignedToBora));  // sorumlu danışman
        Assert.Equal(OperationResult.Success, Check(f.Mert, ayseHidden));          // yönetici
    }

    [Fact]
    public async Task Photos_OfHiddenOrDeletedProperty_AreNotVisible()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Property(f.Ayse, Visibility.OnlyMe);
        var photo = f.AddRow(f.Ayse, hidden);

        Assert.Null(f.Photos.TGetVisible(photo.PropertyPhotoId, f.Bora.Id, false));
        Assert.Null(f.Photos.TGetForProperty(hidden.PropertyId, f.Bora.Id, false));
        Assert.NotNull(f.Photos.TGetVisible(photo.PropertyPhotoId, f.Mert.Id, true));

        f.Properties.TSoftDelete(hidden.PropertyId, f.Ayse.Id, false);
        Assert.Null(f.Photos.TGetVisible(photo.PropertyPhotoId, f.Ayse.Id, false));
        Assert.Null(f.Photos.TGetVisible(photo.PropertyPhotoId, f.Mert.Id, true));
    }

    [Fact]
    public async Task DeleteAndSetCover_RequireEdit_HiddenIs404()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.Property(f.Ayse, Visibility.Public);
        var hidden = f.Property(f.Ayse, Visibility.OnlyMe);
        var openPhoto = f.AddRow(f.Ayse, open);
        var hiddenPhoto = f.AddRow(f.Ayse, hidden);

        Assert.Equal(OperationResult.Forbidden, f.Photos.TDelete(openPhoto.PropertyPhotoId, f.Bora.Id, false, out _, out _));
        Assert.Equal(OperationResult.Forbidden, f.Photos.TSetCover(openPhoto.PropertyPhotoId, f.Bora.Id, false, out _));
        Assert.Equal(OperationResult.NotFound, f.Photos.TDelete(hiddenPhoto.PropertyPhotoId, f.Bora.Id, false, out _, out _));
        Assert.Equal(OperationResult.NotFound, f.Photos.TSetCover(hiddenPhoto.PropertyPhotoId, f.Bora.Id, false, out _));
        Assert.Equal(2, f.Identity.Db.PropertyPhotos.Count());

        Assert.Equal(OperationResult.Success, f.Photos.TDelete(openPhoto.PropertyPhotoId, f.Mert.Id, true, out var pid, out var fileName));
        Assert.Equal(open.PropertyId, pid);
        Assert.Equal(openPhoto.FileName, fileName);
    }

    // ---------------- Kapak ve sınır ----------------

    [Fact]
    public async Task FirstPhotoIsCover_DeletingCoverPromotesNext_SetCoverMovesIt()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var first = f.AddRow(f.Ayse, p);
        var second = f.AddRow(f.Ayse, p);
        var third = f.AddRow(f.Ayse, p);

        List<PropertyPhoto> List() => f.Photos.TGetForProperty(p.PropertyId, f.Ayse.Id, false)!;

        Assert.Equal(first.PropertyPhotoId, Assert.Single(List(), x => x.IsCover).PropertyPhotoId);

        Assert.Equal(OperationResult.Success, f.Photos.TSetCover(third.PropertyPhotoId, f.Ayse.Id, false, out _));
        Assert.Equal(third.PropertyPhotoId, List().First().PropertyPhotoId); // kapak önce gelir
        Assert.Single(List(), x => x.IsCover);

        Assert.Equal(OperationResult.Success, f.Photos.TDelete(third.PropertyPhotoId, f.Ayse.Id, false, out _, out _));
        Assert.Equal(first.PropertyPhotoId, Assert.Single(List(), x => x.IsCover).PropertyPhotoId);
        Assert.Equal(2, List().Count);
        Assert.Contains(List(), x => x.PropertyPhotoId == second.PropertyPhotoId);
    }

    [Fact]
    public async Task Limit_PerProperty_IsEnforced()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        for (var i = 0; i < IPropertyPhotoService.MaxPerProperty; i++)
            f.AddRow(f.Ayse, p);

        Assert.Equal(OperationResult.Invalid, f.Photos.TCheckUpload(p.PropertyId, f.Ayse.Id, false, out var remaining));
        Assert.Equal(0, remaining);
        Assert.Equal(OperationResult.Invalid, f.Photos.TAdd(p.PropertyId, "x.jpg", "image/jpeg", null, f.Ayse.Id, false));
    }

    // ---------------- Controller ----------------

    [Fact]
    public async Task Upload_SavesValidImages_RejectsFakeOnes_FileNameFromServer()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var c = f.Controller(f.Ayse);

        var result = await c.Upload(p.PropertyId, new List<IFormFile>
        {
            Fixture.File(Jpeg, "../../kotu.jpg"),
            Fixture.File(Png, "plan.png"),
            Fixture.File(Html, "foto.jpg"),     // adı jpg ama içeriği HTML
            Fixture.File(Array.Empty<byte>())
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Property", redirect.ControllerName);
        Assert.Equal("fotograflar", redirect.Fragment);

        var rows = f.Identity.Db.PropertyPhotos.OrderBy(x => x.PropertyPhotoId).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "image/jpeg", "image/png" }, rows.Select(r => r.ContentType));
        Assert.All(rows, r => Assert.StartsWith($"{p.PropertyId}/", r.FileName));
        Assert.All(rows, r => Assert.DoesNotContain("kotu", r.FileName));
        Assert.Equal("kotu.jpg", rows[0].OriginalName); // sadece ad, yol kısmı atılır
        Assert.All(rows, r => Assert.NotNull(f.Store.GetPath(r.FileName)));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(f.Root, p.PropertyId.ToString())).Length);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task Upload_AuthBeforeFiles_404And403_NothingWritten()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Property(f.Ayse, Visibility.OnlyMe);
        var open = f.Property(f.Ayse, Visibility.Public);
        var bora = f.Controller(f.Bora);

        Assert.IsType<NotFoundResult>(await bora.Upload(hidden.PropertyId, new List<IFormFile> { Fixture.File(Jpeg) }));
        Assert.IsType<ForbidResult>(await bora.Upload(open.PropertyId, new List<IFormFile> { Fixture.File(Jpeg) }));
        Assert.IsType<NotFoundResult>(await bora.Upload(hidden.PropertyId, null)); // dosya olmasa da önce yetki

        Assert.Empty(f.Identity.Db.PropertyPhotos);
        Assert.False(Directory.Exists(f.Root));
    }

    [Fact]
    public async Task Upload_TooLarge_IsRejected()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        var big = new byte[ImageFileValidator.MaxBytes + 1];
        Jpeg.CopyTo(big, 0);

        await f.Controller(f.Ayse).Upload(p.PropertyId, new List<IFormFile> { Fixture.File(big) });

        Assert.Empty(f.Identity.Db.PropertyPhotos);
    }

    [Fact]
    public async Task Show_ReturnsFile_OnlyToViewers()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Property(f.Ayse, Visibility.OnlyMe);
        await f.Controller(f.Ayse).Upload(hidden.PropertyId, new List<IFormFile> { Fixture.File(Jpeg) });
        var photo = f.Identity.Db.PropertyPhotos.Single();

        var ayse = f.Controller(f.Ayse);
        var file = Assert.IsType<PhysicalFileResult>(ayse.Show(photo.PropertyPhotoId));
        Assert.Equal("image/jpeg", file.ContentType);
        // Her açılışta sunucuya sorulmalı (çıkıştan sonra başka kullanıcı önbellekten yetkisiz göremesin)
        Assert.Equal("private, no-cache", ayse.Response.Headers.CacheControl.ToString());

        Assert.IsType<NotFoundResult>(f.Controller(f.Bora).Show(photo.PropertyPhotoId));
        Assert.IsType<NotFoundResult>(f.Controller(f.Ayse).Show(9999));
        Assert.IsType<PhysicalFileResult>(f.Controller(f.Mert).Show(photo.PropertyPhotoId));
    }

    [Fact]
    public async Task Delete_RemovesRowAndFile_OtherAgentForbidden()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        await f.Controller(f.Ayse).Upload(p.PropertyId, new List<IFormFile> { Fixture.File(Jpeg) });
        var photo = f.Identity.Db.PropertyPhotos.Single();

        Assert.IsType<ForbidResult>(f.Controller(f.Bora).Delete(photo.PropertyPhotoId));
        Assert.NotNull(f.Store.GetPath(photo.FileName));

        Assert.IsType<RedirectToActionResult>(f.Controller(f.Ayse).Delete(photo.PropertyPhotoId));
        Assert.Empty(f.Identity.Db.PropertyPhotos);
        Assert.Null(f.Store.GetPath(photo.FileName));
    }

    [Fact]
    public async Task Detail_ListsPhotoIdsOnly()
    {
        using var f = await Fixture.CreateAsync();
        var p = f.Property(f.Ayse, Visibility.Public);
        f.AddRow(f.Ayse, p);

        var c = f.Identity.SignedInAs(TestControllers.Property(f.Identity.Db, f.Properties,
            new ContactManager(new EfContactDal(f.Identity.Db))), f.Bora, RoleNames.Agent);
        var model = Assert.IsType<PropertyDetailViewModel>(Assert.IsType<ViewResult>(c.Detail(p.PropertyId)).Model);

        Assert.True(Assert.Single(model.Photos).IsCover);
        Assert.False(model.CanEdit); // Bora fotoğrafı görür ama yükleme/silme butonları çıkmaz
        Assert.Equal(new[] { "PropertyPhotoId", "IsCover" }, typeof(PropertyPhotoItemViewModel).GetProperties().Select(x => x.Name));
    }
}
