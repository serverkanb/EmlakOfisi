using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using Microsoft.AspNetCore.Identity;

namespace EmlakOfisi.WebUI.SeedData
{
    // InstrumentTrade'deki IdentityInitializer ile aynı görev: rolleri ve ilk admini oluşturur.
    // Fark: admin kullanıcı adı ve şifresi kodda değil, yapılandırmadan okunur
    // (geliştirmede user-secrets: Seed:AdminUserName, Seed:AdminEmail, Seed:AdminPassword).
    // Roller her ortamda oluşturulur; admin kullanıcı SADECE Development ortamında.
    public static class IdentityInitializer
    {
        public static async Task SeedRolesAndUsersAsync(
            UserManager<AppUser> userManager,
            RoleManager<AppRole> roleManager,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger logger)
        {
            foreach (var role in RoleNames.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new AppRole { Name = role });
                }
            }

            if (!environment.IsDevelopment())
                return;

            var userName = configuration["Seed:AdminUserName"];
            var email = configuration["Seed:AdminEmail"];
            var password = configuration["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogInformation("Seed:AdminUserName / Seed:AdminPassword tanımlı değil, admin kullanıcı oluşturulmadı.");
                return;
            }

            if (await userManager.FindByNameAsync(userName) != null)
                return;

            var admin = new AppUser
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                Name = "Sistem",
                Surname = "Yöneticisi",
                IsActive = true
            };

            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                // Şifre kurallara uymuyorsa uygulama yine açılır, hata loglanır.
                logger.LogWarning("Admin kullanıcı oluşturulamadı: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(admin, RoleNames.Admin);
        }
    }
}
