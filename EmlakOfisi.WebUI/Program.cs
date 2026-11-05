using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.SeedData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------
//  DB Context
// ---------------------------
// Connection string kodda değil: geliştirmede `dotnet user-secrets`, sunucuda ortam değişkeni
// (ConnectionStrings__DefaultConnection) ile verilir.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection bulunamadı. Geliştirmede: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\" --project EmlakOfisi.WebUI");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// ---------------------------
//  Identity Ayarları
// ---------------------------
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    // InstrumentTrade'deki gibi büyük harf / özel karakter zorunlu değil,
    // ama müşteri verisi tutulduğu için en az 8 karakter ve rakam isteniyor.
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    options.User.RequireUniqueEmail = true;

    // 5 hatalı denemede 15 dakika kilit
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddErrorDescriber<TurkishIdentityErrorDescriber>();

// Pasif yapılan ya da rolü değişen kullanıcının açık oturumu en geç 5 dakikada yenilenir/düşer
// (AdminUserController ve RoleAssignController security stamp'i yeniliyor).
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

// ---------------------------
//  Global Authorization
// ---------------------------
// InstrumentTrade'de bu filtre yorum satırındaydı. Burada açık: iç yazılım olduğu için
// tüm sayfalar giriş ister, sadece [AllowAnonymous] olanlar (Login) açıktır.
// Ayrıca tüm POST isteklerinde antiforgery token kontrol edilir.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// ---------------------------
//  Cookie Ayarları
// ---------------------------
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login/Index";
    options.LogoutPath = "/Login/Logout";
    options.AccessDeniedPath = "/Login/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);   // bir iş günü
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

// ---------------------------
//  Middleware
// ---------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

// ---------------------------
//  Rol ve admin seed
// ---------------------------
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await IdentityInitializer.SeedRolesAndUsersAsync(userManager, roleManager, app.Configuration, app.Environment, logger);
}

app.Run();
