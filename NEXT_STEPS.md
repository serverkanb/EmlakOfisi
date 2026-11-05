# NEXT_STEPS

## Tamamlananlar
- 2026-11-05: İlk görev (mimari, entity modeli, roller/görünürlük, mobil yaklaşım, MVP, görev listesi, ilk ekran kabul kriterleri) hazırlandı ve onaylandı → `docs/MIMARI_PLAN.md`
- 2026-11-05: Görev 1 `altyapi: solution ve katman projeleri` – EntityLayer, DataAccessLayer, BusinessLayer, WebUI (MVC) ve Tests (xUnit) projeleri, net9.0. Referanslar WebUI → BL → DAL → Entity, Tests → BL + DAL. Build ve test yeşil (1 iskelet testi).

- 2026-11-05: Görev 2 `entity: identity ve temel entityler` – AppUser/AppRole, 9 enum (sabit int değerli), Contact, Property, PropertyPhoto, CustomerRequest, Appointment, Activity, Transaction. EntityLayer'a `Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.0.22` eklendi. Varsayılan değer testleri (OnlyMe, Active, IsActive) dahil 5/5 test yeşil.

- 2026-11-05: Görev 3 `dal: dbcontext, generic repository, user-secrets`
  - `AppDbContext(DbContextOptions)`. OnConfiguring yok, kendi tablolarımızdaki tüm FK'lar `Restrict`.
  - `IGenericDal`/`GenericRepository` ve `IGenericService` eklendi.
  - DAL'a EF Core SqlServer ve Tools, WebUI'ye Design paketi eklendi (9.0.22). `dotnet-ef` projeye yerel araç olarak eklendi (`.config/dotnet-tools.json`).
  - Connection string user-secrets'ta (LocalDB, `EmlakOfisiDev`).
  - `InitialCreate` migration'ı **oluşturuldu, veritabanına uygulanmadı**.
  - Model testleriyle birlikte 13/13 test yeşil.

## Sıradaki küçük görev
- (Onay bekliyor) `InitialCreate` migration'ını LocalDB'ye uygulamak:
  `dotnet tool run dotnet-ef database update -p EmlakOfisi.DataAccessLayer -s EmlakOfisi.WebUI`
  Yeni ve boş veritabanı oluşturuyor, veri kaybı yok.
- Görev 4 `kimlik: giris, cikis, rol seed`
  - `AddIdentity<AppUser, AppRole>` ve `LoginController` (`LocalRedirect`, IsActive kontrolü).
  - Global `[Authorize]`.
  - `IdentityInitializer`: Admin, Manager ve Agent rolleri. Geliştirme admin şifresi user-secrets'tan okunacak.
  - Cookie ayarları.

## Açık kararlar / notlar
- Mimari, InstrumentTrade projesindeki 4 katmanlı yapıya göre kuruluyor (Generic Dal/Service, EfXDal, XManager). Farkları `docs/MIMARI_PLAN.md` bölüm 0'da anlatılıyor.
- .NET 9 ile başlandı. **Görev 6b'de .NET 10 LTS'ye yükseltilecek** (.NET 9'un desteği 10 Kasım 2026'da bitiyor), o zamana kadar .NET 10 SDK kurulmalı.
- UI: Tabler UI (görev 5'te `wwwroot/lib/tabler` altına eklenecek, şablonun getirdiği varsayılan Bootstrap/Home sayfaları o zaman temizlenecek).
- Yeni bir bilgisayarda ilk kurulum için iki komut çalıştırılır: `dotnet tool restore`, ardından `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<kendi SQL Server'ınız>" --project EmlakOfisi.WebUI`.
- Migration komutları: `dotnet tool run dotnet-ef migrations add <Ad> -p EmlakOfisi.DataAccessLayer -s EmlakOfisi.WebUI -o Migrations`. Visual Studio'daki Package Manager Console da kullanılabilir (Tools paketi DAL'da).
- Tarih alanları InstrumentTrade'deki gibi `DateTime.Now` kullanıyor (tek saat dilimi, Türkiye). Çok bölgeli kullanım gerekirse UTC'ye geçilir.
- AppUser'da ters yönde koleksiyon navigation'ı yok, bilerek eklenmedi: aynı tabloya birden çok FK olduğunda EF'in ilişkileri karıştırmaması için.
- `.gitignore`: fotoğraflar `App_Data/uploads` altında saklanacak, görev 13'te bu klasör eklenecek.
