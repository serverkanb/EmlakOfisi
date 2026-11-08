using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Concrete;
using EmlakOfisi.DataAccessLayer.Context;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Helpers;
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

// Ofis adı ve logo (sır değil; appsettings.json "Office"). Eksik/geçersiz değerler Normalize ile güvenli hale gelir.
builder.Services.AddSingleton((builder.Configuration.GetSection(OfficeSettings.SectionName).Get<OfficeSettings>() ?? new OfficeSettings()).Normalize());

// ---------------------------
//  Katmanlı Servisler (InstrumentTrade'deki gibi: Dal + Service çiftleri)
// ---------------------------
builder.Services.AddScoped<IContactDal, EfContactDal>();
builder.Services.AddScoped<IContactService, ContactManager>();

builder.Services.AddScoped<IActivityDal, EfActivityDal>();
builder.Services.AddScoped<IActivityService, ActivityManager>();

builder.Services.AddScoped<IPropertyDal, EfPropertyDal>();
builder.Services.AddScoped<IPropertyService, PropertyManager>();

builder.Services.AddScoped<IPropertyInterestDal, EfPropertyInterestDal>();
builder.Services.AddScoped<IPropertyInterestService, PropertyInterestManager>();

builder.Services.AddScoped<IPropertyPhotoDal, EfPropertyPhotoDal>();
builder.Services.AddScoped<IPropertyPhotoService, PropertyPhotoManager>();

builder.Services.AddScoped<IAppointmentDal, EfAppointmentDal>();
builder.Services.AddScoped<IAppointmentService, AppointmentManager>();

builder.Services.AddScoped<ICustomerRequestDal, EfCustomerRequestDal>();
builder.Services.AddScoped<ICustomerRequestService, CustomerRequestManager>();

builder.Services.AddScoped<ITransactionDal, EfTransactionDal>();
builder.Services.AddScoped<ITransactionService, TransactionManager>();

// Fotoğraflar wwwroot DIŞINDA saklanır (adresle doğrudan açılamaz). Sunucuda farklı bir klasör
// gerekirse "Uploads:Root" ayarıyla değiştirilir; uygulama havuzunun bu klasöre yazma izni olmalı.
var uploadsRoot = builder.Configuration["Uploads:Root"];
if (string.IsNullOrWhiteSpace(uploadsRoot))
    uploadsRoot = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads");
builder.Services.AddSingleton(new PhotoFileStore(uploadsRoot));

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
.AddErrorDescriber<TurkishIdentityErrorDescriber>()
.AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();   // "şifre değiştirmeli" işareti oturuma

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
    options.Filters.Add(new MustChangePasswordFilter());   // ilk girişte şifre değiştirme zorunluluğu

    // Formlardaki İngilizce varsayılan mesajlar Türkçe (binder ve mesajı yazılmamış doğrulama kuralları)
    TurkishValidationMessages.Apply(options);
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
//  Rol ve admin seed + (sadece Development) test verisi
// ---------------------------
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await IdentityInitializer.SeedRolesAndUsersAsync(userManager, roleManager, app.Configuration, app.Environment, logger);

    var contactService = scope.ServiceProvider.GetRequiredService<IContactService>();
    var propertyService = scope.ServiceProvider.GetRequiredService<IPropertyService>();
    var appointmentService = scope.ServiceProvider.GetRequiredService<IAppointmentService>();
    await TestDataSeeder.SeedAsync(userManager, contactService, propertyService, appointmentService, app.Configuration, app.Environment, logger);
}

app.Run();
