using System.Security.Claims;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmlakOfisi.Tests;

// Testler için gerçek Identity (UserManager) + bellekte çalışan veritabanı.
// Her test kendi boş veritabanını alır.
public sealed class TestIdentity : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public UserManager<AppUser> UserManager { get; }
    public SignInManager<AppUser> SignInManager { get; }
    public AppDbContext Db => _scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private TestIdentity()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddMvcCore(); // controller'daki RedirectToAction için (URL üretici)
        services.AddDataProtection();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        // Şifre/kullanıcı kuralları Program.cs'teki AddIdentity ayarlarının kopyasıdır;
        // orada değişirse burada da güncelleyin.
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddErrorDescriber<EmlakOfisi.WebUI.Extensions.TurkishIdentityErrorDescriber>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        SignInManager = _scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
    }

    public static async Task<TestIdentity> CreateAsync()
    {
        var identity = new TestIdentity();
        var roleManager = identity._scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        foreach (var role in RoleNames.All)
            await roleManager.CreateAsync(new AppRole { Name = role });
        return identity;
    }

    public async Task<AppUser> AddUserAsync(string userName, string role, bool isActive = true)
    {
        var user = new AppUser
        {
            UserName = userName,
            Email = $"{userName}@test.local",
            Name = userName,
            Surname = "Test",
            IsActive = isActive
        };
        var result = await UserManager.CreateAsync(user, "TestSifre123");
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        await UserManager.AddToRoleAsync(user, role);
        return user;
    }

    // Controller'ı verilen kullanıcı giriş yapmış gibi hazırlar
    public T SignedInAs<T>(T controller, AppUser user, params string[] roles) where T : Controller
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName!)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            RequestServices = _scope.ServiceProvider
        };
        _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        controller.ControllerContext = new ControllerContext(
            new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor()));
        controller.TempData = new TempDataDictionary(httpContext, new NullTempDataProvider());
        return controller;
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
