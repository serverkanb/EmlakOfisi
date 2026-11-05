using System.Reflection;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.WebUI.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.Tests;

// Global [Authorize] filtresi Program.cs'te. Bu testler, girişsiz açılabilen yerlerin
// (AllowAnonymous) sadece bilinen sayfalar olduğunu garanti eder. Yeni bir controller veya
// action'a yanlışlıkla [AllowAnonymous] eklenirse test kırılır.
public class AuthorizationRulesTests
{
    private static readonly Assembly WebAssembly = typeof(LoginController).Assembly;

    private static readonly HashSet<string> AllowedAnonymous = new()
    {
        "LoginController",              // giriş, çıkış, yetkisiz erişim sayfası
        "HomeController.Error",         // hata sayfası
    };

    [Fact]
    public void OnlyKnownEndpoints_AllowAnonymous()
    {
        var controllers = WebAssembly.GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract);

        var anonymous = new List<string>();

        foreach (var controller in controllers)
        {
            if (controller.GetCustomAttribute<AllowAnonymousAttribute>() != null)
                anonymous.Add(controller.Name);

            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);

            anonymous.AddRange(actions.Select(a => $"{controller.Name}.{a.Name}"));
        }

        var unexpected = anonymous.Where(a => !AllowedAnonymous.Contains(a)).ToList();
        Assert.True(unexpected.Count == 0, "Beklenmeyen [AllowAnonymous]: " + string.Join(", ", unexpected));
    }

    [Fact]
    public void Logout_IsPostOnly()
    {
        var logout = typeof(LoginController).GetMethod(nameof(LoginController.Logout))!;

        Assert.NotNull(logout.GetCustomAttribute<HttpPostAttribute>());
        Assert.Null(logout.GetCustomAttribute<HttpGetAttribute>());
    }

    [Fact]
    public void Roles_AreAdminManagerAgent()
    {
        Assert.Equal(new[] { "Admin", "Manager", "Agent" }, RoleNames.All);
    }
}
