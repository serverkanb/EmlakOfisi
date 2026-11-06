using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.DataAccessLayer.Abstract;
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
            IPropertyService propertyService,
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
            {
                SeedProperties(propertyService, contactService, ids, logger);
                return;
            }

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

            SeedProperties(propertyService, contactService, ids, logger);
        }

        // Sahte portföyler: sadece hiç (silinmemiş) portföy yoksa. Sahipler yukarıdaki sahte müşterilerden seçilir;
        // iki portföyün sorumlusu müdür tarafından başka bir danışmana atanır ("sorumlu da görür" kuralını denemek için).
        private static void SeedProperties(IPropertyService propertyService, IContactService contactService,
            Dictionary<string, int> ids, ILogger logger)
        {
            var m = ids["mudur1"];
            var (_, existing) = propertyService.TGetVisibleList(m, true, new PropertyFilter(), 1, 1);
            if (existing > 0)
                return;

            var a1 = ids["danisman1"];
            var a2 = ids["danisman2"];

            // Sahibi, o kişiyi gören kullanıcının gözünden bul (servis görünmeyen sahibi kabul etmez)
            int? Owner(int viewer, string fullName) =>
                contactService.TGetVisibleList(viewer, viewer == m, fullName, null, 1, 1).Items.FirstOrDefault()?.ContactId;

            var count = 0;
            void Add(int creator, string title, ListingType listing, PropertyType type, decimal price,
                string city, string district, string? neighborhood, Visibility visibility, int? ownerId,
                PropertyStatus status = PropertyStatus.Active, string? rooms = null, int? net = null,
                decimal? lat = null, decimal? lng = null, int agent = 0)
            {
                var result = propertyService.TCreate(new Property
                {
                    Title = title, ListingType = listing, PropertyType = type, Price = price, Status = status,
                    City = city, District = district, Neighborhood = neighborhood, Visibility = visibility,
                    OwnerContactId = ownerId, RoomCount = rooms, NetArea = net, GrossArea = net == null ? null : net + 15,
                    Latitude = lat, Longitude = lng, AgentUserId = agent,
                    Description = "Sahte deneme kaydıdır."
                }, creator, canSeeAll: creator == m);
                if (result == OperationResult.Success) count++;
                else logger.LogWarning("Sahte portföy eklenemedi: {Title} ({Result})", title, result);
            }

            // danisman1 (Ahmet)
            Add(a1, "Moda 3+1 Deniz Manzaralı Daire", ListingType.Sale, PropertyType.Residence, 8_500_000,
                "İstanbul", "Kadıköy", "Moda", Visibility.Public, Owner(a1, "Ayşe Çelik"), rooms: "3+1", net: 125,
                lat: 40.984900m, lng: 29.026200m);
            Add(a1, "Kadıköy 2+1 Kiralık Daire", ListingType.Rent, PropertyType.Residence, 32_000,
                "İstanbul", "Kadıköy", "Fenerbahçe", Visibility.OnlyMe, Owner(a1, "Deniz Arslan"), rooms: "2+1", net: 90);
            Add(a1, "Ataşehir Satılık Ofis", ListingType.Sale, PropertyType.Commercial, 12_000_000,
                "İstanbul", "Ataşehir", null, Visibility.OnlyMe, Owner(a1, "Fatma Kılıç"), PropertyStatus.Sold, net: 140);

            // danisman2 (Zeynep)
            Add(a2, "Urla Bağ Evi", ListingType.Sale, PropertyType.Residence, 15_000_000,
                "İzmir", "Urla", "Kuşçular", Visibility.OnlyMe, Owner(a2, "Murat Polat"), rooms: "4+1", net: 210);
            Add(a2, "Bornova 1+1 Öğrenciye Uygun", ListingType.Rent, PropertyType.Residence, 15_000,
                "İzmir", "Bornova", "Kazımdirik", Visibility.Public, Owner(a2, "Ufuk Bulut"), PropertyStatus.Rented, "1+1", 55);

            // mudur1 (Merve): sorumluyu danışmanlara atar
            Add(m, "Gölbaşı İmarlı Arsa", ListingType.Sale, PropertyType.Land, 4_750_000,
                "Ankara", "Gölbaşı", null, Visibility.OnlyMe, Owner(m, "Zafer Ateş"), agent: a2);
            Add(m, "Çankaya Cadde Üstü Dükkan", ListingType.Rent, PropertyType.Commercial, 45_000,
                "Ankara", "Çankaya", "Kızılay", Visibility.Public, Owner(m, "Sibel Acar"), net: 80, agent: a1);
            Add(m, "Nilüfer Havuzlu Villa", ListingType.Sale, PropertyType.Residence, 22_000_000,
                "Bursa", "Nilüfer", null, Visibility.Public, null, PropertyStatus.Passive, "5+2", 320);

            logger.LogInformation("{Count} sahte portföy oluşturuldu.", count);
        }
    }
}
