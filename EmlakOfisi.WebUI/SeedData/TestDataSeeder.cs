using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using Microsoft.AspNetCore.Identity;

namespace EmlakOfisi.WebUI.SeedData
{
    // SADECE Development: deneme kullanıcıları ve SAHTE müşteriler (gerçek veri yok, CLAUDE.md kural 7).
    // Kullanıcı şifresi kodda değil: user-secrets "Seed:TestUserPassword". Tanımlı değilse hiçbir şey yapılmaz.
    // Kişiler sadece müşteri tablosu tamamen boşsa eklenir (her açılışta tekrar eklenmez).
    public static class TestDataSeeder
    {
        private record TestUser(string UserName, string Name, string Surname, string Role);

        private static readonly TestUser[] Users =
        {
            new("mudur1", "Merve", "Kaya", RoleNames.Manager),
            new("danisman1", "Ahmet", "Yıldız", RoleNames.Agent),
            new("danisman2", "Zeynep", "Demir", RoleNames.Agent),
        };

        public static string FakePhone(int n) => $"0100 {n:000} {n % 100:00} {(n * 7) % 100:00}";

        public static async Task SeedAsync(
            UserManager<AppUser> userManager,
            IContactService contactService,
            IConfiguration configuration,
            IHostEnvironment environment,
            ILogger logger)
        {
            if (!environment.IsDevelopment())
                return;

            var password = configuration["Seed:TestUserPassword"];
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogInformation("Seed:TestUserPassword tanımlı değil, test kullanıcıları ve sahte müşteriler oluşturulmadı.");
                return;
            }

            var ids = new Dictionary<string, int>();
            foreach (var u in Users)
            {
                var user = await userManager.FindByNameAsync(u.UserName);
                if (user == null)
                {
                    user = new AppUser
                    {
                        UserName = u.UserName,
                        Email = $"{u.UserName}@emlakofisi.test",
                        EmailConfirmed = true,
                        Name = u.Name,
                        Surname = u.Surname,
                        IsActive = true
                    };
                    var result = await userManager.CreateAsync(user, password);
                    if (!result.Succeeded)
                    {
                        logger.LogWarning("Test kullanıcısı {User} oluşturulamadı: {Errors}", u.UserName,
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                        return;
                    }
                }

                // Rol eksikse (ilk atama başarısız olduysa da) tamamla
                if (!await userManager.IsInRoleAsync(user, u.Role))
                {
                    var roleResult = await userManager.AddToRoleAsync(user, u.Role);
                    if (!roleResult.Succeeded)
                        logger.LogWarning("Test kullanıcısı {User} için {Role} rolü atanamadı.", u.UserName, u.Role);
                }

                ids[u.UserName] = user.Id;
            }

            // Silinmemiş müşteri varsa dokunma (hepsi silinmişse sahte kişiler yeniden eklenir; sadece geliştirme)
            var (_, existing) = contactService.TGetVisibleList(ids["mudur1"], canSeeAll: true, null, null, 1, 1);
            if (existing > 0)
                return;

            var a1 = ids["danisman1"];
            var a2 = ids["danisman2"];
            var m = ids["mudur1"];
            var n = 0;

            void Add(int owner, string name, string surname, Visibility visibility,
                bool buyer = false, bool seller = false, bool tenant = false, bool landlord = false, string? notes = null)
            {
                n++;
                contactService.TCreate(new Contact
                {
                    Name = name,
                    Surname = surname,
                    // SAHTE numara: 0100 hiçbir operatöre atanmamış bir önek; Ara/WhatsApp gerçek birine gitmez.
                    // (0555 gibi gerçek operatör önekleri kullanılmaz.)
                    Phone = FakePhone(n),
                    Visibility = visibility,
                    IsBuyer = buyer, IsSeller = seller, IsTenant = tenant, IsLandlord = landlord,
                    Notes = notes
                }, owner);
            }

            // danisman1 (Ahmet): 6 gizli, 5 herkese açık
            Add(a1, "Ali", "Işık", Visibility.OnlyMe, buyer: true, notes: "3+1 daire arıyor, bütçe esnek.");
            Add(a1, "Ayşe", "Çelik", Visibility.OnlyMe, seller: true, landlord: true);
            Add(a1, "Burak", "Şahin", Visibility.OnlyMe, tenant: true);
            Add(a1, "Canan", "Öztürk", Visibility.OnlyMe, buyer: true, seller: true);
            Add(a1, "Deniz", "Arslan", Visibility.OnlyMe, landlord: true);
            Add(a1, "Emre", "Doğan", Visibility.OnlyMe, buyer: true);
            Add(a1, "Fatma", "Kılıç", Visibility.Public, seller: true);
            Add(a1, "Gökhan", "Aydın", Visibility.Public, tenant: true);
            Add(a1, "Hülya", "Koç", Visibility.Public, buyer: true, tenant: true);
            Add(a1, "İnci", "Kurt", Visibility.Public, landlord: true);
            Add(a1, "Kemal", "Özdemir", Visibility.Public, buyer: true);

            // danisman2 (Zeynep): 5 gizli, 5 herkese açık
            Add(a2, "Leyla", "Aksoy", Visibility.OnlyMe, buyer: true, notes: "Sadece Zeynep ve yöneticiler görmeli.");
            Add(a2, "Murat", "Polat", Visibility.OnlyMe, seller: true);
            Add(a2, "Nur", "Erdoğan", Visibility.OnlyMe, tenant: true, landlord: true);
            Add(a2, "Oğuz", "Güneş", Visibility.OnlyMe, buyer: true);
            Add(a2, "Pınar", "Yalçın", Visibility.OnlyMe, landlord: true);
            Add(a2, "Rıza", "Uçar", Visibility.Public, seller: true);
            Add(a2, "Selin", "Tekin", Visibility.Public, buyer: true);
            Add(a2, "Tolga", "Kaplan", Visibility.Public, tenant: true);
            Add(a2, "Ufuk", "Bulut", Visibility.Public, landlord: true, seller: true);
            Add(a2, "Yasemin", "Işıklı", Visibility.Public, buyer: true);

            // mudur1 (Merve): 2 gizli, 2 herkese açık
            Add(m, "Zafer", "Ateş", Visibility.OnlyMe, seller: true);
            Add(m, "Ece", "Kara", Visibility.OnlyMe, buyer: true);
            Add(m, "Hakan", "Uysal", Visibility.Public, tenant: true);
            Add(m, "Sibel", "Acar", Visibility.Public, landlord: true);

            logger.LogInformation("{Count} sahte müşteri oluşturuldu.", n);
        }
    }
}
