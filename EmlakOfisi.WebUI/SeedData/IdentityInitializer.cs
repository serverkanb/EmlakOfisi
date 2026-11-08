using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using Microsoft.AspNetCore.Identity;

namespace EmlakOfisi.WebUI.SeedData
{
    // InstrumentTrade'deki IdentityInitializer ile aynı görev: rolleri ve ilk admini oluşturur.
    // Fark: admin kullanıcı adı ve şifresi kodda değil, yapılandırmadan okunur
    // (geliştirmede user-secrets: Seed:AdminUserName, Seed:AdminEmail, Seed:AdminPassword).
    // Roller her ortamda oluşturulur. Geliştirme admini SADECE Development ortamında (Seed:*);
    // canlı sistemde ilk admin sunucu ayarından bir kez oluşturulur (Bootstrap:*, BootstrapFirstAdminAsync).
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

            if (environment.IsDevelopment())
                await SeedDevelopmentAdminAsync(userManager, configuration, logger);

            await BootstrapFirstAdminAsync(userManager, configuration, logger);
        }

        // CANLI SİSTEM (her ortam): sistemde hiç Admin yoksa, sunucu ayarındaki bilgilerle ilk admin BİR KEZ oluşturulur
        // (ortam değişkenleri: Bootstrap__AdminUserName, Bootstrap__AdminEmail, Bootstrap__AdminPassword).
        // Bu admin ilk girişte şifresini değiştirmek zorundadır. Admin varsa ayar yok sayılır, mevcut hesaba dokunulmaz.
        public static async Task BootstrapFirstAdminAsync(UserManager<AppUser> userManager, IConfiguration configuration, ILogger logger)
        {
            var userName = configuration["Bootstrap:AdminUserName"];
            var email = configuration["Bootstrap:AdminEmail"];
            var password = configuration["Bootstrap:AdminPassword"];
            var configured = !string.IsNullOrWhiteSpace(userName) || !string.IsNullOrWhiteSpace(password) || !string.IsNullOrWhiteSpace(email);

            if ((await userManager.GetUsersInRoleAsync(RoleNames.Admin)).Count > 0)
            {
                if (configured)
                    logger.LogWarning("Sistemde zaten admin var; Bootstrap ayarları yok sayıldı. Güvenlik için Bootstrap__AdminUserName/Email/Password ortam değişkenlerini sunucudan silin.");
                return;
            }

            // Üçü de zorunlu (e-posta da: Identity benzersiz e-posta istiyor, boş e-postayla kullanıcı oluşmaz)
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning("Sistemde hiç admin yok ve Bootstrap:AdminUserName / Bootstrap:AdminEmail / Bootstrap:AdminPassword ayarlarından en az biri tanımlı değil; kimse giriş yapamaz. İlk kurulum için bu ortam değişkenlerini tanımlayıp uygulamayı yeniden başlatın.");
                return;
            }

            // Aynı adla rolsüz bir kullanıcı kalmışsa (önceki denemede rol ataması başarısız olduysa) yenisi açılamaz
            if (await userManager.FindByNameAsync(userName.Trim()) != null)
            {
                logger.LogError("İlk admin oluşturulamadı: '{UserName}' adlı bir kullanıcı zaten var ama Admin rolü yok. Başka bir Bootstrap__AdminUserName verin ya da bu kullanıcıya veritabanından Admin rolü atayın.", userName.Trim());
                return;
            }

            var admin = new AppUser
            {
                UserName = userName.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true,
                Name = "Sistem",
                Surname = "Yöneticisi",
                IsActive = true,
                MustChangePassword = true
            };

            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                logger.LogError("İlk admin oluşturulamadı: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }

            var roleResult = await userManager.AddToRoleAsync(admin, RoleNames.Admin);
            if (!roleResult.Succeeded)
            {
                // Rolsüz kullanıcı kalmasın: bir sonraki açılışta temiz deneme yapılabilsin
                await userManager.DeleteAsync(admin);
                logger.LogError("İlk admin'e Admin rolü atanamadı, kullanıcı geri alındı: {Errors}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogWarning("İlk admin oluşturuldu ({UserName}). Giriş yapıp şifreyi değiştirin, ardından Bootstrap ortam değişkenlerini sunucudan silin.", admin.UserName);
        }

        // SADECE Development: user-secrets'taki geliştirme admini (şifre değiştirme zorunluluğu yok)
        private static async Task SeedDevelopmentAdminAsync(UserManager<AppUser> userManager, IConfiguration configuration, ILogger logger)
        {
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
