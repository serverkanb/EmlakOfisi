using System.Security.Claims;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Models;
using EmlakOfisi.WebUI.SeedData;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EmlakOfisi.Tests;

// Canlı sistemde ilk admin (sunucu ayarından bir kez) ve ilk girişte şifre değiştirme zorunluluğu.
public class FirstAdminTests
{
    private const string Pass = "IlkSifre123";

    private static IConfiguration Config(string? user = "ilkadmin", string? password = Pass, string? email = "admin@ofis.test") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:AdminUserName"] = user,
            ["Bootstrap:AdminEmail"] = email,
            ["Bootstrap:AdminPassword"] = password
        }).Build();

    [Fact]
    public async Task NoAdmin_WithConfig_CreatesAdmin_MustChangePassword()
    {
        using var identity = await TestIdentity.CreateAsync();
        await IdentityInitializer.BootstrapFirstAdminAsync(identity.UserManager, Config(), NullLogger.Instance);

        var admin = await identity.UserManager.FindByNameAsync("ilkadmin");
        Assert.NotNull(admin);
        Assert.True(admin!.MustChangePassword);
        Assert.True(admin.IsActive);
        Assert.True(await identity.UserManager.IsInRoleAsync(admin, RoleNames.Admin));
        Assert.True(await identity.UserManager.CheckPasswordAsync(admin, Pass));
    }

    [Fact]
    public async Task AdminExists_ConfigIgnored_ExistingUntouched()
    {
        using var identity = await TestIdentity.CreateAsync();
        var existing = await identity.AddUserAsync("eskiadmin", RoleNames.Admin);
        var stamp = await identity.UserManager.GetSecurityStampAsync(existing);

        await IdentityInitializer.BootstrapFirstAdminAsync(identity.UserManager, Config(user: "eskiadmin", password: "BaskaSifre99"), NullLogger.Instance);
        await IdentityInitializer.BootstrapFirstAdminAsync(identity.UserManager, Config(user: "yeniadmin"), NullLogger.Instance);

        Assert.Null(await identity.UserManager.FindByNameAsync("yeniadmin"));
        var reloaded = (await identity.UserManager.FindByNameAsync("eskiadmin"))!;
        Assert.False(await identity.UserManager.CheckPasswordAsync(reloaded, "BaskaSifre99"));
        Assert.Equal(stamp, await identity.UserManager.GetSecurityStampAsync(reloaded));
        Assert.False(reloaded.MustChangePassword);
    }

    [Theory]
    [InlineData(null, Pass)]          // kullanıcı adı yok
    [InlineData("ilkadmin", null)]    // şifre yok
    [InlineData("ilkadmin", "kisa")]  // kurallara uymuyor
    [InlineData("ilkadmin", "sadeceharfler")]
    [InlineData("ilkadmin", Pass, "")]      // e-posta yok (Identity benzersiz e-posta istiyor)
    public async Task MissingOrWeakConfig_NoAdminCreated(string? user, string? password, string? email = "admin@ofis.test")
    {
        using var identity = await TestIdentity.CreateAsync();
        await IdentityInitializer.BootstrapFirstAdminAsync(identity.UserManager, Config(user, password, email), NullLogger.Instance);
        Assert.Empty(await identity.UserManager.GetUsersInRoleAsync(RoleNames.Admin));
    }

    [Fact]
    public async Task SameNameUserWithoutRole_NotTakenOver()
    {
        using var identity = await TestIdentity.CreateAsync();
        var existing = await identity.AddUserAsync("ilkadmin", RoleNames.Agent);   // aynı adda, Admin olmayan kullanıcı

        await IdentityInitializer.BootstrapFirstAdminAsync(identity.UserManager, Config(), NullLogger.Instance);

        Assert.Empty(await identity.UserManager.GetUsersInRoleAsync(RoleNames.Admin));
        var reloaded = (await identity.UserManager.FindByNameAsync("ilkadmin"))!;
        Assert.False(await identity.UserManager.CheckPasswordAsync(reloaded, Pass));   // şifresi değişmedi
        Assert.Equal(new[] { RoleNames.Agent }, await identity.UserManager.GetRolesAsync(reloaded));
        _ = existing;
    }

    // ---------------- Oturum işareti ve yönlendirme ----------------

    [Fact]
    public async Task ClaimsFactory_AddsFlag_OnlyWhenRequired()
    {
        using var identity = await TestIdentity.CreateAsync();
        var roles = identity.RoleManager;
        var factory = new AppClaimsPrincipalFactory(identity.UserManager, roles, Options.Create(new IdentityOptions()));

        var normal = await identity.AddUserAsync("normal", RoleNames.Agent);
        Assert.False((await factory.CreateAsync(normal)).MustChange());

        normal.MustChangePassword = true;
        await identity.UserManager.UpdateAsync(normal);
        Assert.True((await factory.CreateAsync(normal)).MustChange());
    }

    private static ActionExecutingContext Context(string controller, string action, bool mustChange, bool authenticated = true)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        if (mustChange) claims.Add(new Claim(MustChangePassword.ClaimType, "true"));
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(authenticated ? new ClaimsIdentity(claims, "Test") : new ClaimsIdentity())
        };
        var descriptor = new ControllerActionDescriptor { ControllerName = controller, ActionName = action };
        return new ActionExecutingContext(new ActionContext(http, new RouteData(), descriptor),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    [Theory]
    [InlineData("Dashboard", "Index")]
    [InlineData("Contact", "Index")]
    [InlineData("Property", "Detail")]
    [InlineData("AdminUser", "Index")]
    [InlineData("Profile", "Other")]
    public void Filter_RedirectsFlaggedUser(string controller, string action)
    {
        var context = Context(controller, action, mustChange: true);
        new MustChangePasswordFilter().OnActionExecuting(context);
        var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
        Assert.Equal(("Profile", "ChangePassword"), (redirect.ControllerName, redirect.ActionName));
    }

    [Theory]
    [InlineData("Profile", "ChangePassword", true)]
    [InlineData("Login", "Logout", true)]
    [InlineData("Error", "Index", true)]
    [InlineData("Manifest", "Index", true)]
    [InlineData("Dashboard", "Index", false)]   // işaretsiz kullanıcı
    public void Filter_AllowsExemptPages_AndNormalUsers(string controller, string action, bool mustChange)
    {
        var context = Context(controller, action, mustChange);
        new MustChangePasswordFilter().OnActionExecuting(context);
        Assert.Null(context.Result);
    }

    [Fact]
    public void Filter_IgnoresAnonymous()
    {
        var context = Context("Dashboard", "Index", mustChange: false, authenticated: false);
        new MustChangePasswordFilter().OnActionExecuting(context);
        Assert.Null(context.Result);
    }

    // ---------------- Şifre değişince işaret kalkar; admin'in açtığı hesap işaretli ----------------

    [Fact]
    public async Task ChangePassword_ClearsFlag()
    {
        using var identity = await TestIdentity.CreateAsync();
        var user = await identity.AddUserAsync("ayse", RoleNames.Agent);
        user.MustChangePassword = true;
        await identity.UserManager.UpdateAsync(user);

        var controller = identity.SignedInAs(new ProfileController(identity.UserManager, identity.SignInManager), user, RoleNames.Agent);
        var result = await controller.ChangePassword(new ChangePasswordViewModel
        {
            CurrentPassword = "TestSifre123", NewPassword = "YeniSifre456", ConfirmPassword = "YeniSifre456"
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False((await identity.UserManager.FindByNameAsync("ayse"))!.MustChangePassword);
    }

    [Fact]
    public async Task ChangePassword_SamePassword_Rejected_FlagKept()
    {
        using var identity = await TestIdentity.CreateAsync();
        var user = await identity.AddUserAsync("ayse", RoleNames.Agent);
        user.MustChangePassword = true;
        await identity.UserManager.UpdateAsync(user);

        var controller = identity.SignedInAs(new ProfileController(identity.UserManager, identity.SignInManager), user, RoleNames.Agent);
        var result = await controller.ChangePassword(new ChangePasswordViewModel
        {
            CurrentPassword = "TestSifre123", NewPassword = "TestSifre123", ConfirmPassword = "TestSifre123"
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True((await identity.UserManager.FindByNameAsync("ayse"))!.MustChangePassword);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_FlagKept()
    {
        using var identity = await TestIdentity.CreateAsync();
        var user = await identity.AddUserAsync("ayse", RoleNames.Agent);
        user.MustChangePassword = true;
        await identity.UserManager.UpdateAsync(user);

        var controller = identity.SignedInAs(new ProfileController(identity.UserManager, identity.SignInManager), user, RoleNames.Agent);
        Assert.IsType<ViewResult>(await controller.ChangePassword(new ChangePasswordViewModel
        {
            CurrentPassword = "YanlisSifre1", NewPassword = "YeniSifre456", ConfirmPassword = "YeniSifre456"
        }));
        Assert.True((await identity.UserManager.FindByNameAsync("ayse"))!.MustChangePassword);
    }

}
