using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

public class AdminUserControllerTests
{
    [Fact]
    public async Task Create_AddsActiveUser_WithSelectedRole()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var controller = identity.SignedInAs(new AdminUserController(identity.UserManager), admin, RoleNames.Admin);

        var result = await controller.Create(new AdminUserCreateViewModel
        {
            Name = "Ayşe",
            Surname = "Yılmaz",
            UserName = "ayse.yilmaz",
            Email = "ayse@test.local",
            Password = "Gecici1234",
            Role = RoleNames.Agent
        });

        Assert.IsType<RedirectToActionResult>(result);
        var created = await identity.UserManager.FindByNameAsync("ayse.yilmaz");
        Assert.NotNull(created);
        Assert.True(created!.IsActive);
        Assert.True(created.MustChangePassword);   // admin şifreyi biliyor: ilk girişte kullanıcı kendi şifresini koyar
        Assert.Equal(new[] { RoleNames.Agent }, await identity.UserManager.GetRolesAsync(created));
    }

    [Fact]
    public async Task Create_RejectsUnknownRole()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var controller = identity.SignedInAs(new AdminUserController(identity.UserManager), admin, RoleNames.Admin);

        var result = await controller.Create(new AdminUserCreateViewModel
        {
            Name = "X", Surname = "Y", UserName = "xy", Email = "xy@test.local",
            Password = "Gecici1234", Role = "SuperUser"
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Null(await identity.UserManager.FindByNameAsync("xy"));
    }

    [Fact]
    public async Task Create_RejectsWeakPassword()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var controller = identity.SignedInAs(new AdminUserController(identity.UserManager), admin, RoleNames.Admin);

        var result = await controller.Create(new AdminUserCreateViewModel
        {
            Name = "X", Surname = "Y", UserName = "xy", Email = "xy@test.local",
            Password = "kisa", Role = RoleNames.Agent
        });

        Assert.IsType<ViewResult>(result);
        Assert.Null(await identity.UserManager.FindByNameAsync("xy"));
    }

    [Fact]
    public async Task ToggleActive_Deactivates_AndChangesSecurityStamp()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var stampBefore = agent.SecurityStamp;
        var controller = identity.SignedInAs(new AdminUserController(identity.UserManager), admin, RoleNames.Admin);

        await controller.ToggleActive(agent.Id);

        var reloaded = await identity.UserManager.FindByIdAsync(agent.Id.ToString());
        Assert.False(reloaded!.IsActive);
        Assert.NotEqual(stampBefore, reloaded.SecurityStamp); // açık oturum düşecek
    }

    [Fact]
    public async Task ToggleActive_TwiceReactivates()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var controller = identity.SignedInAs(new AdminUserController(identity.UserManager), admin, RoleNames.Admin);

        await controller.ToggleActive(agent.Id);
        await controller.ToggleActive(agent.Id);

        Assert.True((await identity.UserManager.FindByIdAsync(agent.Id.ToString()))!.IsActive);
    }

    [Fact]
    public async Task ToggleActive_CannotDeactivateSelf()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var controller = identity.SignedInAs(new AdminUserController(identity.UserManager), admin, RoleNames.Admin);

        await controller.ToggleActive(admin.Id);

        Assert.True((await identity.UserManager.FindByIdAsync(admin.Id.ToString()))!.IsActive);
        Assert.Equal("Kendi hesabınızı pasif yapamazsınız.", controller.TempData["Error"]);
    }

    [Fact]
    public async Task ToggleActive_UnknownUser_ReturnsNotFound()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var controller = identity.SignedInAs(new AdminUserController(identity.UserManager), admin, RoleNames.Admin);

        Assert.IsType<NotFoundResult>(await controller.ToggleActive(9999));
    }
}
