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
        "ErrorController",              // hata sayfası
        "ManifestController",           // PWA manifest'i (tarayıcı çerezsiz ister; sadece ad, renk, ikon)
    };

    [Fact]
    public void OnlyKnownEndpoints_AllowAnonymous()
    {
        var controllers = WebAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

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

    [Theory]
    [InlineData(typeof(AdminUserController))]
    [InlineData(typeof(RoleAssignController))]
    public void AdminControllers_RequireAdminRole(Type controller)
    {
        var attribute = controller.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(RoleNames.Admin, attribute!.Roles);
    }

    // Rol şartı olmayabilecek (sadece giriş yeterli) controller'lar. Bunların dışındaki her controller
    // ya Admin ya da Staff (ofis çalışanı) rolü istemeli; rolsüz kullanıcı iş verisine erişemesin.
    private static readonly HashSet<string> NoRoleRequired = new()
    {
        "LoginController", "ErrorController", "ProfileController", "ManifestController"
    };

    [Fact]
    public void BusinessControllers_RequireStaffOrAdminRole()
    {
        var missing = WebAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract && !NoRoleRequired.Contains(t.Name))
            .Where(t =>
            {
                var roles = t.GetCustomAttribute<AuthorizeAttribute>()?.Roles;
                return roles != RoleNames.Staff && roles != RoleNames.Admin;
            })
            .Select(t => t.Name)
            .ToList();

        Assert.True(missing.Count == 0, "Rol şartı olmayan controller: " + string.Join(", ", missing));
    }

    [Fact]
    public void ContactController_RequiresStaffRole()
    {
        Assert.Equal(RoleNames.Staff, typeof(ContactController).GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [Fact]
    public void StateChangingActions_ArePostOnly()
    {
        // Durum değiştiren işlemler GET ile çağrılamamalı (link ile tetiklenemesin)
        var actions = new[]
        {
            typeof(AdminUserController).GetMethod(nameof(AdminUserController.ToggleActive))!,
            typeof(AdminUserController).GetMethods().Single(m => m.Name == nameof(AdminUserController.Create) && m.GetParameters().Length == 1),
            typeof(RoleAssignController).GetMethods().Single(m => m.Name == nameof(RoleAssignController.AssignRole) && m.GetParameters().Length == 2),
            typeof(ProfileController).GetMethods().Single(m => m.Name == nameof(ProfileController.ChangePassword) && m.GetParameters().Length == 1),
            typeof(ContactController).GetMethods().Single(m => m.Name == nameof(ContactController.Create) && m.GetParameters().Length == 1),
            typeof(ContactController).GetMethods().Single(m => m.Name == nameof(ContactController.Edit) && m.GetParameters().Length == 2),
            typeof(ContactController).GetMethod(nameof(ContactController.Delete))!,
            typeof(ActivityController).GetMethod(nameof(ActivityController.Create))!,
            typeof(ActivityController).GetMethod(nameof(ActivityController.Delete))!,
            typeof(PropertyController).GetMethods().Single(m => m.Name == nameof(PropertyController.Create) && m.GetParameters().Length == 1),
            typeof(PropertyController).GetMethods().Single(m => m.Name == nameof(PropertyController.Edit) && m.GetParameters().Length == 2),
            typeof(PropertyController).GetMethod(nameof(PropertyController.Delete))!,
            typeof(PropertyInterestController).GetMethod(nameof(PropertyInterestController.Add))!,
            typeof(PropertyInterestController).GetMethod(nameof(PropertyInterestController.Delete))!,
            typeof(PropertyPhotoController).GetMethod(nameof(PropertyPhotoController.Upload))!,
            typeof(PropertyPhotoController).GetMethod(nameof(PropertyPhotoController.Delete))!,
            typeof(PropertyPhotoController).GetMethod(nameof(PropertyPhotoController.SetCover))!,
            typeof(AppointmentController).GetMethods().Single(m => m.Name == nameof(AppointmentController.Create) && m.GetParameters().Length == 1),
            typeof(AppointmentController).GetMethods().Single(m => m.Name == nameof(AppointmentController.Edit) && m.GetParameters().Length == 2),
            typeof(AppointmentController).GetMethod(nameof(AppointmentController.SetStatus))!,
            typeof(CustomerRequestController).GetMethods().Single(m => m.Name == nameof(CustomerRequestController.Create) && m.GetParameters().Length == 2),
            typeof(CustomerRequestController).GetMethods().Single(m => m.Name == nameof(CustomerRequestController.Edit) && m.GetParameters().Length == 2),
            typeof(CustomerRequestController).GetMethod(nameof(CustomerRequestController.SetStatus))!,
            typeof(TransactionController).GetMethods().Single(m => m.Name == nameof(TransactionController.Create) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(EmlakOfisi.WebUI.Models.TransactionFormViewModel)),
            typeof(TransactionController).GetMethods().Single(m => m.Name == nameof(TransactionController.Edit) && m.GetParameters().Length == 2),
            typeof(TransactionController).GetMethod(nameof(TransactionController.SetStatus))!,
            typeof(UserPermissionController).GetMethods().Single(m => m.Name == nameof(UserPermissionController.Edit) && m.GetParameters().Length == 2),
        };

        foreach (var action in actions)
        {
            Assert.True(action.GetCustomAttribute<HttpPostAttribute>() != null, $"{action.DeclaringType!.Name}.{action.Name} POST olmalı");
        }
    }

    [Fact]
    public void Controllers_DoNotUseDataAccessLayerDirectly()
    {
        // Controller'lar veriye sadece servisler (I...Service) üzerinden erişir; Dal'ı doğrudan alan
        // bir controller görünürlük ve yetki kurallarını atlayabilir.
        var dalAssembly = typeof(EmlakOfisi.DataAccessLayer.Context.AppDbContext).Assembly;

        // Controller ve ViewComponent'lerin constructor'ları ile public metot (action/Invoke) parametreleri taranır
        // ([FromServices] ile action'a enjekte edilen Dal da yakalanır).
        var types = WebAssembly.GetTypes()
            .Where(t => !t.IsAbstract &&
                        (typeof(ControllerBase).IsAssignableFrom(t) || typeof(ViewComponent).IsAssignableFrom(t)));

        var offenders = types
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters())
                .Concat(t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters()))
                .Where(p => p.ParameterType.Assembly == dalAssembly)
                .Select(p => $"{t.Name}({p.ParameterType.Name})"))
            .ToList();

        Assert.True(offenders.Count == 0, "Dal/DbContext alan controller/ViewComponent: " + string.Join(", ", offenders));
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
