using System.ComponentModel.DataAnnotations;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Görev 9: ekleme / düzenleme / silme. Görmediği kayıt 404, görüp düzenleyemediği kayıt 403.
// Kullanıcılar: Ayşe ve Bora (danışman), Mert (yönetici).
public class ContactCrudTests
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
            return new Fixture
            {
                Identity = identity,
                Service = new ContactManager(new EfContactDal(identity.Db)),
                Ayse = await identity.AddUserAsync("ayse", RoleNames.Agent),
                Bora = await identity.AddUserAsync("bora", RoleNames.Agent),
                Mert = await identity.AddUserAsync("mert", RoleNames.Manager)
            };
        }

        public Contact Add(AppUser owner, string name, Visibility visibility, string phone)
        {
            var c = new Contact { Name = name, Surname = "Test", Phone = phone, Visibility = visibility };
            Service.TCreate(c, owner.Id);
            return c;
        }

        public ContactController As(AppUser user, string role) =>
            Identity.SignedInAs(new ContactController(Service), user, role);

        public Contact? Read(int id) => Service.TGetVisibleById(id, Mert.Id, canSeeAll: true);

        public void Dispose() => Identity.Dispose();
    }

    private static ContactFormViewModel Form(string name = "Yeni", string phone = "0100 900 00 01",
        Visibility visibility = Visibility.OnlyMe) => new()
    {
        Name = name, Surname = "Kisi", Phone = phone, Visibility = visibility, IsBuyer = true
    };

    // ---------------- Ekleme ----------------

    [Fact]
    public async Task Create_Get_DefaultVisibilityIsOnlyMe()
    {
        using var f = await Fixture.CreateAsync();
        var model = Assert.IsType<ContactFormViewModel>(Assert.IsType<ViewResult>(f.As(f.Ayse, RoleNames.Agent).Create()).Model);
        Assert.Equal(Visibility.OnlyMe, model.Visibility);
    }

    [Fact]
    public async Task Create_Post_SetsOwnerToCurrentUser_AndRedirectsToDetail()
    {
        using var f = await Fixture.CreateAsync();

        var redirect = Assert.IsType<RedirectToActionResult>(f.As(f.Ayse, RoleNames.Agent).Create(Form("Fatma")));

        var id = (int)redirect.RouteValues!["id"]!;
        var saved = f.Read(id)!;
        Assert.Equal("Fatma", saved.Name);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
        Assert.Equal(Visibility.OnlyMe, saved.Visibility);
        Assert.Null(f.Service.TGetVisibleById(id, f.Bora.Id, false)); // başka danışman görmez
    }

    [Fact]
    public async Task Create_Post_InvalidPhone_ReturnsFormWithError()
    {
        using var f = await Fixture.CreateAsync();
        var controller = f.As(f.Ayse, RoleNames.Agent);

        Assert.IsType<ViewResult>(controller.Create(Form(phone: "12345")));
        Assert.True(controller.ModelState.ContainsKey(nameof(ContactFormViewModel.Phone)));
    }

    [Fact]
    public async Task Create_Post_InvalidVisibility_IsRejected()
    {
        using var f = await Fixture.CreateAsync();
        var controller = f.As(f.Ayse, RoleNames.Agent);

        Assert.IsType<ViewResult>(controller.Create(Form(visibility: (Visibility)99)));
        Assert.True(controller.ModelState.ContainsKey(nameof(ContactFormViewModel.Visibility)));
        Assert.Empty(f.Identity.Db.Contacts); // kayıt oluşmadı
    }

    [Theory]
    [InlineData("05a32 111 22 33")]   // harf (rakam sayısı 10 olsa bile)
    [InlineData("0532 111 22 3x")]
    [InlineData("+90 532 111 22 33")] // "+" simgesi
    public async Task Create_Post_PhoneWithLetters_IsRejected_WithMessage(string phone)
    {
        using var f = await Fixture.CreateAsync();
        var controller = f.As(f.Ayse, RoleNames.Agent);

        Assert.IsType<ViewResult>(controller.Create(Form(phone: phone)));
        Assert.Contains("Telefona sadece rakam girilebilir.",
            controller.ModelState[nameof(ContactFormViewModel.Phone)]!.Errors.Select(e => e.ErrorMessage));
        Assert.Empty(f.Identity.Db.Contacts);
    }

    [Fact]
    public async Task Create_Post_SecondPhoneWithLetters_IsRejected()
    {
        using var f = await Fixture.CreateAsync();
        var controller = f.As(f.Ayse, RoleNames.Agent);
        var form = Form();
        form.Phone2 = "0532abc";

        Assert.IsType<ViewResult>(controller.Create(form));
        Assert.True(controller.ModelState.ContainsKey(nameof(ContactFormViewModel.Phone2)));
    }

    [Theory]
    [InlineData("0532 111 22 333")]   // fazla hane: kırpılıp kabul edilmemeli
    [InlineData("532 111 22 3")]      // eksik hane
    [InlineData("1 0532 111 22 33")]  // 0/90 dışında önekli 12 hane
    public async Task Create_Post_WrongDigitCount_IsRejected(string phone)
    {
        using var f = await Fixture.CreateAsync();
        var controller = f.As(f.Ayse, RoleNames.Agent);

        Assert.IsType<ViewResult>(controller.Create(Form(phone: phone)));
        Assert.Empty(f.Identity.Db.Contacts);
    }

    [Fact]
    public async Task Create_Post_TrimsValues_AndEmptyOptionalFieldsBecomeNull()
    {
        using var f = await Fixture.CreateAsync();
        var form = Form("  Ali  ");
        form.Surname = "  Veli ";
        form.Email = "   ";
        form.Notes = "";
        form.Phone2 = " ";

        var id = (int)Assert.IsType<RedirectToActionResult>(f.As(f.Ayse, RoleNames.Agent).Create(form)).RouteValues!["id"]!;

        var saved = f.Read(id)!;
        Assert.Equal("Ali", saved.Name);
        Assert.Equal("Veli", saved.Surname);
        Assert.Null(saved.Email);
        Assert.Null(saved.Notes);
        Assert.Null(saved.Phone2);
    }

    [Fact]
    public async Task Create_Post_DuplicateOfVisibleRecord_ShowsLink()
    {
        using var f = await Fixture.CreateAsync();
        var existing = f.Add(f.Bora, "Mevcut", Visibility.Public, "0100 900 00 01");
        var controller = f.As(f.Ayse, RoleNames.Agent);

        var view = Assert.IsType<ViewResult>(controller.Create(Form(phone: "901009000001")));

        Assert.Equal(existing.ContactId, view.ViewData["DuplicateContactId"]);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Create_Post_DuplicateOfHiddenRecord_GenericMessage_NoLeak()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Bora, "GizliKisi", Visibility.OnlyMe, "0100 900 00 01");
        var controller = f.As(f.Ayse, RoleNames.Agent);

        var view = Assert.IsType<ViewResult>(controller.Create(Form()));

        Assert.Null(view.ViewData["DuplicateContactId"]); // id verilmez
        var errors = controller.ModelState[nameof(ContactFormViewModel.Phone)]!.Errors.Select(e => e.ErrorMessage);
        Assert.Single(errors);
        Assert.DoesNotContain("GizliKisi", errors.Single()); // ad verilmez
        Assert.DoesNotContain("bora", errors.Single());
    }

    // ---------------- Düzenleme ----------------

    [Fact]
    public async Task Edit_Get_Own_ShowsForm()
    {
        using var f = await Fixture.CreateAsync();
        var own = f.Add(f.Ayse, "Benim", Visibility.OnlyMe, "0100 900 00 02");

        var model = Assert.IsType<ContactFormViewModel>(Assert.IsType<ViewResult>(f.As(f.Ayse, RoleNames.Agent).Edit(own.ContactId)).Model);
        Assert.Equal("Benim", model.Name);
    }

    [Fact]
    public async Task Edit_Get_OthersPublic_IsForbidden()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.Add(f.Bora, "Acik", Visibility.Public, "0100 900 00 03");

        Assert.IsType<ForbidResult>(f.As(f.Ayse, RoleNames.Agent).Edit(open.ContactId));
    }

    [Fact]
    public async Task Edit_Get_OthersOnlyMe_IsNotFound()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Add(f.Bora, "Gizli", Visibility.OnlyMe, "0100 900 00 04");

        Assert.IsType<NotFoundResult>(f.As(f.Ayse, RoleNames.Agent).Edit(hidden.ContactId));
    }

    [Fact]
    public async Task Edit_Post_OthersPublic_IsForbidden_AndNothingChanges()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.Add(f.Bora, "Acik", Visibility.Public, "0100 900 00 03");

        Assert.IsType<ForbidResult>(f.As(f.Ayse, RoleNames.Agent).Edit(open.ContactId, Form("Degisti", "0100 900 00 03")));
        Assert.Equal("Acik", f.Read(open.ContactId)!.Name);
    }

    [Fact]
    public async Task Edit_Post_OthersOnlyMe_IsNotFound_EvenWithInvalidForm()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Add(f.Bora, "Gizli", Visibility.OnlyMe, "0100 900 00 04");

        // Form geçersiz olsa da önce yetki kontrol edilir; kaydın varlığı form hatasıyla belli olmaz
        Assert.IsType<NotFoundResult>(f.As(f.Ayse, RoleNames.Agent).Edit(hidden.ContactId, Form(phone: "1")));
    }

    [Fact]
    public async Task Edit_Post_Own_Succeeds_OwnerUnchanged()
    {
        using var f = await Fixture.CreateAsync();
        var own = f.Add(f.Ayse, "Benim", Visibility.OnlyMe, "0100 900 00 02");

        // Aynı telefonla kaydetmek (kendisi) tekrar kayıt sayılmaz
        Assert.IsType<RedirectToActionResult>(
            f.As(f.Ayse, RoleNames.Agent).Edit(own.ContactId, Form("Yenilendi", "0100 900 00 02", Visibility.Public)));

        var saved = f.Read(own.ContactId)!;
        Assert.Equal("Yenilendi", saved.Name);
        Assert.Equal(Visibility.Public, saved.Visibility);
        Assert.Equal(f.Ayse.Id, saved.CreatedByUserId);
    }

    [Fact]
    public async Task Edit_Post_PhoneOfAnotherRecord_IsRejected()
    {
        using var f = await Fixture.CreateAsync();
        f.Add(f.Ayse, "Birinci", Visibility.OnlyMe, "0100 900 00 05");
        var second = f.Add(f.Ayse, "Ikinci", Visibility.OnlyMe, "0100 900 00 06");
        var controller = f.As(f.Ayse, RoleNames.Agent);

        Assert.IsType<ViewResult>(controller.Edit(second.ContactId, Form("Ikinci", "0100 900 00 05")));
        Assert.Equal("1009000006", f.Read(second.ContactId)!.PhoneNormalized); // değişmedi
    }

    [Fact]
    public async Task Edit_Post_Manager_CanEditOthersOnlyMe()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Add(f.Bora, "Gizli", Visibility.OnlyMe, "0100 900 00 04");

        Assert.IsType<RedirectToActionResult>(f.As(f.Mert, RoleNames.Manager).Edit(hidden.ContactId, Form("Mudur", "0100 900 00 04")));
        Assert.Equal(f.Bora.Id, f.Read(hidden.ContactId)!.CreatedByUserId);
    }

    // ---------------- Silme ----------------

    [Fact]
    public async Task Delete_Own_HidesRecord()
    {
        using var f = await Fixture.CreateAsync();
        var own = f.Add(f.Ayse, "Benim", Visibility.OnlyMe, "0100 900 00 07");

        Assert.IsType<RedirectToActionResult>(f.As(f.Ayse, RoleNames.Agent).Delete(own.ContactId));
        Assert.Null(f.Read(own.ContactId));
        Assert.True(f.Identity.Db.Contacts.Single(c => c.ContactId == own.ContactId).IsDeleted); // satır duruyor
    }

    [Fact]
    public async Task Delete_OthersPublic_IsForbidden()
    {
        using var f = await Fixture.CreateAsync();
        var open = f.Add(f.Bora, "Acik", Visibility.Public, "0100 900 00 08");

        Assert.IsType<ForbidResult>(f.As(f.Ayse, RoleNames.Agent).Delete(open.ContactId));
        Assert.NotNull(f.Read(open.ContactId));
    }

    [Fact]
    public async Task Delete_OthersOnlyMe_IsNotFound()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Add(f.Bora, "Gizli", Visibility.OnlyMe, "0100 900 00 09");

        Assert.IsType<NotFoundResult>(f.As(f.Ayse, RoleNames.Agent).Delete(hidden.ContactId));
        Assert.NotNull(f.Read(hidden.ContactId));
    }

    [Fact]
    public async Task Delete_Manager_CanDeleteOthers()
    {
        using var f = await Fixture.CreateAsync();
        var hidden = f.Add(f.Bora, "Gizli", Visibility.OnlyMe, "0100 900 00 10");

        Assert.IsType<RedirectToActionResult>(f.As(f.Mert, RoleNames.Manager).Delete(hidden.ContactId));
        Assert.Null(f.Read(hidden.ContactId));
    }

    [Fact]
    public async Task Delete_AlreadyDeleted_IsNotFound()
    {
        using var f = await Fixture.CreateAsync();
        var own = f.Add(f.Ayse, "Benim", Visibility.OnlyMe, "0100 900 00 11");
        var controller = f.As(f.Ayse, RoleNames.Agent);

        controller.Delete(own.ContactId);
        Assert.IsType<NotFoundResult>(controller.Delete(own.ContactId));
    }

    // ---------------- Form modeli ----------------

    [Fact]
    public void FormModel_HasNoOwnerOrStateFields()
    {
        // Over-posting: formdan sahip / silinme / tarih bilgisi gönderilemesin
        var props = typeof(ContactFormViewModel).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("CreatedByUserId", props);
        Assert.DoesNotContain("IsDeleted", props);
        Assert.DoesNotContain("CreatedAt", props);
        Assert.DoesNotContain("ContactId", props);
    }

    [Theory]
    [InlineData("ali@ornek.com", true)]
    [InlineData("gecersiz-eposta", false)]
    [InlineData(null, true)]
    public void FormModel_EmailValidation(string? email, bool valid)
    {
        var model = Form();
        model.Email = email;

        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.Equal(valid, isValid);
    }
}
