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
  - `InitialCreate` migration'ı oluşturuldu, kullanıcı onayıyla LocalDB'ye uygulandı.
  - Model testleriyle birlikte 13/13 test yeşil.

- 2026-11-05: Görev 4 `kimlik: giris, cikis, rol seed`
  - `AddIdentity` ayarları: en az 8 karakter ve rakam zorunlu; 5 hatalı denemede 15 dakika kilit; benzersiz e-posta.
  - Global `[Authorize]` ve `AutoValidateAntiforgeryToken` açık. Cookie ayarları: 8 saat, kaydırmalı süre, HttpOnly, Secure.
  - `LoginController`:
    - Giriş tek akışta: önce `CheckPasswordSignInAsync` ile şifre ve kilit kontrol ediliyor, sonra kullanıcının aktif olup olmadığına bakılıyor, en son `SignInAsync` ile giriş yapılıyor. Pasif hesapta da hatalı denemeler sayılıyor.
    - `returnUrl` sadece site içine yönlendiriyor.
    - Çıkış POST ile yapılıyor.
    - `AccessDenied` sayfası var.
  - `IdentityInitializer`: Admin, Manager ve Agent rolleri her ortamda, her açılışta kontrol ediliyor. Admin kullanıcı **sadece Development ortamında** oluşturuluyor; bilgileri yapılandırmadan okunuyor (`Seed:AdminUserName`, `Seed:AdminEmail`, `Seed:AdminPassword`), geliştirmede bunlar user-secrets'ta.
  - `RoleNames` sabitleri eklendi. Tests projesi artık WebUI'ye de bağlı. `AuthorizationRulesTests` testleri, girişsiz açılabilen sayfaların sadece Login ve Error olduğunu kontrol ediyor.
  - 16/16 test yeşil. curl ile elle denendi:
    - Girişsiz istek → login'e yönleniyor.
    - Token'sız POST → 400.
    - Dış `returnUrl` → ana sayfaya düşüyor.
    - GET ile çıkış → 404.
    - Giriş ve çıkış çalışıyor.
    - Pasif hesap doğru şifreyle giremiyor ve oturum açılmıyor. Pasif hesapta yanlış şifre, hatalı deneme sayacını artırıyor.

## Sıradaki küçük görev
- Görev 5 `arayuz: tabler layout ve menuler`
  - Tabler UI statik dosyaları `wwwroot/lib/tabler` altına eklenecek.
  - `_Layout` ile `LayoutSidebar` ve `LayoutNavbar` ViewComponent'leri, mobil alt menü.
  - Boş Dashboard, Tabler görünümlü Login sayfası.
  - Şablonun getirdiği Home/Privacy sayfaları ve kullanılmayan `wwwroot/lib` dosyaları temizlenecek.

## Açık kararlar / notlar
- Mimari, InstrumentTrade projesindeki 4 katmanlı yapıya göre kuruluyor (Generic Dal/Service, EfXDal, XManager). Farkları `docs/MIMARI_PLAN.md` bölüm 0'da anlatılıyor.
- .NET 9 ile başlandı. **Görev 6b'de .NET 10 LTS'ye yükseltilecek** (.NET 9'un desteği 10 Kasım 2026'da bitiyor), o zamana kadar .NET 10 SDK kurulmalı.
- UI: Tabler UI (görev 5'te `wwwroot/lib/tabler` altına eklenecek, şablonun getirdiği varsayılan Bootstrap/Home sayfaları o zaman temizlenecek).
- Yeni bir bilgisayarda ilk kurulum için iki komut çalıştırılır: `dotnet tool restore`, ardından `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<kendi SQL Server'ınız>" --project EmlakOfisi.WebUI`.
- Migration komutları: `dotnet tool run dotnet-ef migrations add <Ad> -p EmlakOfisi.DataAccessLayer -s EmlakOfisi.WebUI -o Migrations`. Visual Studio'daki Package Manager Console da kullanılabilir (Tools paketi DAL'da).
- Tarih alanları InstrumentTrade'deki gibi `DateTime.Now` kullanıyor (tek saat dilimi, Türkiye). Çok bölgeli kullanım gerekirse UTC'ye geçilir.
- AppUser'da ters yönde koleksiyon navigation'ı yok, bilerek eklenmedi: aynı tabloya birden çok FK olduğunda EF'in ilişkileri karıştırmaması için.
- Bu bilgisayarda HTTPS geliştirme sertifikası yok. `https` profili sertifika olmadığı için açılmıyor, bu yüzden yerel denemeler `http` profiliyle yapılıyor (`.claude/launch.json`). HTTPS ile çalıştırmak için kullanıcının şu komutu kendisinin çalıştırması gerekiyor: `dotnet dev-certs https --trust`.
- Cookie `Secure` olarak işaretli. Bu yüzden telefondan LAN IP'si üzerinden http ile girilirse giriş yapılamaz. Telefonda deneme için HTTPS gerekiyor.
- Production'da ilk admin kullanıcı nasıl oluşturulacak, henüz kararı verilmedi (seed sadece Development'ta çalışıyor). Yayına almadan önce karar verilmeli.
- Giriş akışının otomatik testi yok, şimdilik elle denendi. Uçtan uca HTTP testleri gerekirse `Microsoft.AspNetCore.Mvc.Testing` paketi önerilecek; görünürlük testleri için görev 7'de değerlendirilecek.
- Seed admin şifresini görmek için: `dotnet user-secrets list --project EmlakOfisi.WebUI`. Kullanıcı adı `admin`. Şifre rastgele üretildi.
- `.gitignore`: fotoğraflar `App_Data/uploads` altında saklanacak, görev 13'te bu klasör eklenecek.
