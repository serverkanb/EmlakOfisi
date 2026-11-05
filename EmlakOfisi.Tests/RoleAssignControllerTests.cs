using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

public class RoleAssignControllerTests
{
    private static List<RoleAssignViewModel> Roles(params string[] selected) =>
        RoleNames.All.Select(r => new RoleAssignViewModel { RoleName = r, RoleExist = selected.Contains(r) }).ToList();

    [Fact]
    public async Task AssignRole_Get_ShowsCurrentRoles()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var controller = identity.SignedInAs(new RoleAssignController(identity.UserManager, identity.SignInManager), admin, RoleNames.Admin);

        var view = Assert.IsType<ViewResult>(await controller.AssignRole(agent.Id));
        var model = Assert.IsType<AssignRolePageViewModel>(view.Model);

        Assert.Equal(new[] { RoleNames.Agent }, model.Roles.Where(r => r.RoleExist).Select(r => r.RoleName));
    }

    [Fact]
    public async Task AssignRole_Post_ChangesRoles_AndSecurityStamp()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var stampBefore = agent.SecurityStamp;
        var controller = identity.SignedInAs(new RoleAssignController(identity.UserManager, identity.SignInManager), admin, RoleNames.Admin);

        await controller.AssignRole(agent.Id, Roles(RoleNames.Manager));

        var reloaded = await identity.UserManager.FindByIdAsync(agent.Id.ToString());
        Assert.Equal(new[] { RoleNames.Manager }, await identity.UserManager.GetRolesAsync(reloaded!));
        Assert.NotEqual(stampBefore, reloaded!.SecurityStamp);
    }

    [Fact]
    public async Task AssignRole_Post_IgnoresUnknownRoleNames()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var controller = identity.SignedInAs(new RoleAssignController(identity.UserManager, identity.SignInManager), admin, RoleNames.Admin);

        var roles = Roles(RoleNames.Agent);
        roles.Add(new RoleAssignViewModel { RoleName = "SuperUser", RoleExist = true });
        await controller.AssignRole(agent.Id, roles);

        Assert.Equal(new[] { RoleNames.Agent }, await identity.UserManager.GetRolesAsync(agent));
    }

    [Fact]
    public async Task AssignRole_Post_AdminCannotRemoveOwnAdminRole()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var controller = identity.SignedInAs(new RoleAssignController(identity.UserManager, identity.SignInManager), admin, RoleNames.Admin);

        // Admin kutusunu kaldırıp Manager ekliyor
        await controller.AssignRole(admin.Id, Roles(RoleNames.Manager));

        var roles = await identity.UserManager.GetRolesAsync(admin);
        Assert.Contains(RoleNames.Admin, roles);     // kaldırılmadı
        Assert.Contains(RoleNames.Manager, roles);   // diğer değişiklik uygulandı
        Assert.NotNull(controller.TempData["Error"]);
    }

    [Fact]
    public async Task AssignRole_Post_RequiresAtLeastOneRole()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var controller = identity.SignedInAs(new RoleAssignController(identity.UserManager, identity.SignInManager), admin, RoleNames.Admin);

        await controller.AssignRole(agent.Id, Roles());

        Assert.Equal(new[] { RoleNames.Agent }, await identity.UserManager.GetRolesAsync(agent));
        Assert.NotNull(controller.TempData["Error"]);
    }

    [Fact]
    public async Task AssignRole_Post_AdminAddsRoleToSelf_RefreshesSignIn()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var controller = identity.SignedInAs(new RoleAssignController(identity.UserManager, identity.SignInManager), admin, RoleNames.Admin);

        // Kendi rolünü değiştiren admin: RefreshSignInAsync yolu hata vermeden çalışmalı
        var result = await controller.AssignRole(admin.Id, Roles(RoleNames.Admin, RoleNames.Manager));

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(new[] { RoleNames.Admin, RoleNames.Manager }, (await identity.UserManager.GetRolesAsync(admin)).OrderBy(r => r));
    }

    [Fact]
    public async Task AssignRole_UnknownUser_ReturnsNotFound()
    {
        using var identity = await TestIdentity.CreateAsync();
        var admin = await identity.AddUserAsync("admin", RoleNames.Admin);
        var controller = identity.SignedInAs(new RoleAssignController(identity.UserManager, identity.SignInManager), admin, RoleNames.Admin);

        Assert.IsType<NotFoundResult>(await controller.AssignRole(9999));
        Assert.IsType<NotFoundResult>(await controller.AssignRole(9999, Roles()));
    }
}
