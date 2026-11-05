using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.WebUI.Controllers;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

public class ProfileControllerTests
{
    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ShowsTurkishError_AndKeepsOldPassword()
    {
        using var identity = await TestIdentity.CreateAsync();
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var controller = identity.SignedInAs(new ProfileController(identity.UserManager, identity.SignInManager), agent, RoleNames.Agent);

        var result = await controller.ChangePassword(new ChangePasswordViewModel
        {
            CurrentPassword = "Yanlis12345",
            NewPassword = "YeniSifre123",
            ConfirmPassword = "YeniSifre123"
        });

        Assert.IsType<ViewResult>(result);
        var errors = controller.ModelState[""]!.Errors.Select(e => e.ErrorMessage);
        Assert.Contains("Mevcut şifre yanlış.", errors);
        Assert.True(await identity.UserManager.CheckPasswordAsync(agent, "TestSifre123"));
    }

    [Fact]
    public async Task ChangePassword_Success_NewPasswordWorks()
    {
        using var identity = await TestIdentity.CreateAsync();
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var controller = identity.SignedInAs(new ProfileController(identity.UserManager, identity.SignInManager), agent, RoleNames.Agent);

        var result = await controller.ChangePassword(new ChangePasswordViewModel
        {
            CurrentPassword = "TestSifre123",
            NewPassword = "YeniSifre123",
            ConfirmPassword = "YeniSifre123"
        });

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(await identity.UserManager.CheckPasswordAsync(agent, "YeniSifre123"));
        Assert.False(await identity.UserManager.CheckPasswordAsync(agent, "TestSifre123"));
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_IsRejected()
    {
        using var identity = await TestIdentity.CreateAsync();
        var agent = await identity.AddUserAsync("danisman", RoleNames.Agent);
        var controller = identity.SignedInAs(new ProfileController(identity.UserManager, identity.SignInManager), agent, RoleNames.Agent);

        var result = await controller.ChangePassword(new ChangePasswordViewModel
        {
            CurrentPassword = "TestSifre123",
            NewPassword = "kisa",
            ConfirmPassword = "kisa"
        });

        Assert.IsType<ViewResult>(result);
        Assert.True(await identity.UserManager.CheckPasswordAsync(agent, "TestSifre123"));
    }
}
