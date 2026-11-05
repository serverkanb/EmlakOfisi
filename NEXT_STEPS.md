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

- 2026-11-05: Görev 5 `arayuz: tabler layout ve menuler`
  - Kullanıcı onayıyla jsdelivr'dan indirildi: Tabler core 1.6.1 (`wwwroot/lib/tabler`, css/js) ve Tabler Icons 3.49.0 (`wwwroot/lib/tabler-icons`, css + woff2). Tabler JS Bootstrap 5'i içerdiği için `lib/bootstrap` silindi.
  - `_Layout`: masaüstünde koyu sol menü ve üst çubuk; mobilde üstte menü butonu, altta 4 butonlu sabit menü.
  - ViewComponent'ler: `LayoutSidebar`, `LayoutNavbar` (kullanıcı adı, rol, çıkış) ve `LayoutBottomNav`.
  - Henüz yapılmamış modüller menüde "Yakında" etiketiyle pasif duruyor. Yönetim bölümü sadece Admin'e görünüyor (bu sadece görüntü; asıl kontrol görev 6'daki controller'larda olacak).
  - `HomeController` kaldırıldı. Yerine `DashboardController` (varsayılan sayfa) ve `ErrorController` (`/Error`, girişsiz açılabilir) geldi. Login, AccessDenied ve Error sayfaları Tabler görünümünde.
  - İnceleme düzeltmeleri: Error sayfası menüsüz, kendi başına açılıyor (veritabanına bağlı değil). Mobilde kullanıcı menüsü düğmesi en az 44px. Baş harfler tr-TR kültürüyle büyütülüyor.
  - 16/16 test yeşil. 375px ve 1280px genişlikte görsel kontrol yapıldı, yatay kaydırma yok.
  - Not: Tabler LICENSE dosyaları indirilmedi; MIT lisans notu CSS/JS dosyalarının başında duruyor. İkon CSS'i `.woff`/`.ttf` de istiyor ama sadece `.woff2` indirildi; güncel tarayıcılarda sorun yok.
  - Geliştirme veritabanındaki admin, user-secrets'taki yeni şifreyle yeniden oluşturuldu (kullanıcı onayıyla eski admin satırı silindi).

- 2026-11-06: Görev 6 `yonetim: kullanici ve rol atama`
  - `AdminUserController` (sadece Admin): kullanıcı listesi, yeni kullanıcı (ViewModel, rol seçimi), aktif/pasif (POST). Pasif yapınca security stamp yenileniyor. Admin kendini pasif yapamıyor.
  - `RoleAssignController` (sadece Admin): kullanıcı id'si adresten geliyor (TempData yok). Sadece tanımlı 3 rol atanabiliyor. Admin kendi Admin rolünü kaldıramıyor. Rol değişince stamp yenileniyor; kişi kendi rolünü değiştirdiyse oturumu hemen yenileniyor. InstrumentTrade'deki rol ekleme/silme ekranı (RoleController) bilinçli olarak yok, çünkü roller sabit.
  - `ProfileController.ChangePassword`: herkes kendi şifresini değiştirebiliyor (kullanıcı menüsünde "Şifre değiştir").
  - Identity hata mesajları Türkçe (`TurkishIdentityErrorDescriber`). İşlem sonrası mesajlar `_Alerts` partial'ı ile gösteriliyor.
  - Menü: "Kullanıcılar ve roller" aktif. Rol atamaya kullanıcı listesindeki "Roller" butonuyla gidiliyor.
  - Testler: Tests projesine `Microsoft.EntityFrameworkCore.InMemory 9.0.22` eklendi (planda onaylı). `TestIdentity` yardımcısı, gerçek UserManager/SignInManager'ı bellekteki veritabanıyla çalıştırıyor. Controller, şifre değiştirme ve yetki kuralı testleriyle 36/36 yeşil.
  - İnceleme düzeltmeleri:
    - Rol atama ve kaydetme sonuçları kontrol ediliyor, başarısızsa hata mesajı gösteriliyor.
    - Kullanıcıya en az bir rol seçilmesi zorunlu.
    - DefaultError ve ConcurrencyFailure mesajları da Türkçe.
    - Mobilde içerik butonları en az 44px.
    - Bilerek bırakılan: kullanıcı listesinde her kullanıcı için ayrı rol sorgusu atılıyor (N+1). Kullanıcı sayısı az olduğu için sorun değil.
  - Uçtan uca (curl, LocalDB) denendi:
    - Danışman `/AdminUser`, `/RoleAssign` ve POST ToggleActive'e giremiyor (AccessDenied); menüde Yönetim bölümünü görmüyor.
    - Zayıf şifre Türkçe hata mesajıyla reddediliyor. Yanlış mevcut şifreyle şifre değiştirilemiyor; şifre değişince oturum açık kalıyor, eski şifre geçmiyor, yeni şifre geçiyor.
    - Rol değiştirme ve geri alma çalışıyor. Admin kendini pasif yapamıyor, kendi Admin rolünü kaldıramıyor. Danışman pasif yapılabiliyor.
    - Türkçe karakterler doğru kaydediliyor.
  - Geliştirme veritabanında iki test kullanıcısı kaldı (`deneme.danisman20513`, `deneme.utf8…`), ikisi de **pasif**. Şifreleri kaydedilmedi. Biri betik hatası yüzünden soyadı bozuk ("Dan%FD%FEman"); bu uygulama hatası değil.
  - Deneme sırasında betik hatası yüzünden admin'e yanlışlıkla Manager rolü eklendi; uygulamanın kendi ekranından geri alındı. Admin'de şu an sadece Admin rolü var.

## Sıradaki küçük görev
- Görev 6b `altyapi: net10 yukseltme`. Önce .NET 10 SDK kurulmalı (kullanıcı kuracak). Kurulmadıysa görev 7'ye geçilir, yükseltme sonraya kalır.
- Görev 7 `musteri: gorunurluk sorgulari ve testleri`.

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
- Seed admin şifresini görmek için: `dotnet user-secrets list --project EmlakOfisi.WebUI`. Kullanıcı adı `admin`.
- Dikkat: seed sadece kullanıcı **yoksa** oluşturur. user-secrets'taki şifreyi değiştirmek var olan admin'i etkilemez. Yeni şifrenin geçerli olması için ya geliştirme veritabanındaki admin silinir (sonraki açılışta yeniden oluşur) ya da şifre değiştirme ekranı kullanılır (görev 6).
- Bu bilgisayarda sanal bellek çok dolu (2026-11-06: 17,9 GB'ın 0,4 GB'ı boştu). Derleme "Out of memory" verebiliyor. Önce `dotnet build-server shutdown` denenir. Gerekirse Windows sayfa dosyası büyütülür ya da Visual Studio/Chrome kapatılır (bu kararı kullanıcı verir).
- LocalDB bazen kendiliğinden duruyor. Uygulama "SQL Server process failed to start" hatasıyla açılmazsa önce `sqllocaldb start MSSQLLocalDB` çalıştırılır.
- `.gitignore`: fotoğraflar `App_Data/uploads` altında saklanacak, görev 13'te bu klasör eklenecek.
