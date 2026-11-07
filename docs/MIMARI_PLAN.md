# Emlak Ofisi – İlk Görev Planı (mimari, model, yetki, MVP)

## Bağlam
NEXT_STEPS.md'deki ilk görev: kod yazmadan önce mimariyi, veri modelini, rol ve görünürlük tasarımını, mobil yaklaşımı, MVP kapsamını, görev listesini ve ilk ekranın kabul kriterlerini ortaya koymak. Kullanıcı projenin, alışık olduğu **InstrumentTrade** projesine (C:\Users\ceren\source\repos\InstrumentTrade, sadece okundu) olabildiğince benzer kurulmasını istiyor. Bu plan onaylanana kadar hiçbir dosya oluşturulmayacak.

---

## 0. InstrumentTrade'den aldıklarım

| Konu | InstrumentTrade'de | Emlak projesinde |
|---|---|---|
| Solution | 4 proje: `EntityLayer`, `DataAccessLayer`, `BusinessLayer`, `WebUI` | Aynısı: `EmlakOfisi.EntityLayer`, `.DataAccessLayer`, `.BusinessLayer`, `.WebUI` (+ test projesi, aşağıda) |
| Entity | `Entities/` klasörü, DataAnnotations (`[Required]`, `[StringLength]`), anahtar adı `ProductId`, `OrderId` | Aynısı: `ContactId`, `PropertyId`… |
| DAL | `IGenericDal<T>` + `GenericRepository<T>` (GetList/GetById/Create/Update/Delete), `Abstract/IProductDal`, `Concrete/EfProductDal : GenericRepository<Product>` | Aynısı |
| İş katmanı | `IGenericService<T>` (TGetList, TGetById, TCreate…), `Abstract/IProductService`, `Concrete/ProductManager` | Aynısı (T önekli metotlar dahil) |
| DbContext | `Context/AppDbContext : IdentityDbContext<AppUser, AppRole, int>`, migration'lar DAL projesinde | Aynısı |
| Identity | `AppUser : IdentityUser<int>` (Name, Surname, IsActive), `AppRole : IdentityRole<int>`, `UserManager/SignInManager/RoleManager` | Aynısı |
| DI | Program.cs'te `AddScoped<IXDal, EfXDal>()` + `AddScoped<IXService, XManager>()` | Aynısı |
| Controller | Constructor injection, `[Authorize(Roles = "Admin")]`, Update POST'ta "kaydı DB'den çek, alanları tek tek kopyala, kaydet" | Aynısı |
| Giriş | `LoginController` (PasswordSignInAsync + IsActive kontrolü + Logout), `ConfigureApplicationCookie` ile LoginPath | Aynısı |
| Rol yönetimi | `RoleController`, `RoleAssignController` (+ `RoleAssignViewModel`), `AdminUserController` (aktif/pasif) | Aynısı |
| Seed | `SeedData/IdentityInitializer` ile rol ve admin oluşturma | Aynısı |
| Layout | Layout view'ı + `ViewComponents/AdminLayout/...SidebarViewComponent`, `...NavbarViewComponent` | Aynısı (ad: `LayoutSidebar`, `LayoutNavbar`) |
| ViewModel | `Models/` ve `Areas/Customer/Models/` içinde `XViewModel` | `Models/` içinde `XViewModel` |
| Silme onayı | SweetAlert2 (CDN) | Aynısı |
| İkonlar | Tabler Icons (`ti ti-...`), Modernize içinden | Tabler Icons (Tabler UI içinden) |

**Taşınmayanlar:** `AppDbContext.OnConfiguring` içindeki connection string ve sunucu adı, `IdentityInitializer` içindeki kullanıcı ve şifre bilgileri, migration'lar, veriler, eflyer/colorlib/Modernize dosyaları. Ayrıca kullanılmayan paketler (`FluentValidation`, `MailKit`, `Microsoft.AspNetCore.Identity 2.3.1`, CodeGeneration) eklenmeyecek.

### InstrumentTrade'den farklı yapacaklarım (gerekçeleriyle)
1. **Connection string kodda olmayacak.** InstrumentTrade'de `OnConfiguring` içinde sabit yazılı. Burada `AppDbContext(DbContextOptions<AppDbContext> options)` constructor'ı kullanılacak, Program.cs'te `builder.Configuration.GetConnectionString("DefaultConnection")` ile okunacak, değeri `dotnet user-secrets`'tan gelecek (CLAUDE.md kural 5). Migration komutları aynı kalır (`-s EmlakOfisi.WebUI` ile).
2. **Görünürlük filtresi sorgunun içinde.** InstrumentTrade `OrderList`'te tüm siparişleri `TGetList()` ile çekip controller'da `.Where(o => o.UserId == user.Id)` yapıyor. Burada filtre DAL sorgusunun içinde olacak (`EfContactDal.GetVisibleList(userId, canSeeAll, search)`). Ayrıca `IContactService` ve `IPropertyService`, `IGenericService<T>`'den **türemeyecek**. Böylece controller'ın filtresiz `TGetList()` / `TGetById()` çağırması mümkün olmayacak. Diğer entity'lerde (`Activity` vb.) generic yapı aynen kalacak.
3. **Global `[Authorize]` açık.** InstrumentTrade'de global filtre yorum satırında. Bu bir iç yazılım olduğu için tüm sayfalar varsayılan olarak giriş istiyor, sadece `LoginController` `[AllowAnonymous]` olacak. Dışarıya açık `RegisterController` olmayacak, kullanıcıları Admin oluşturacak.
4. **Formlar entity yerine ViewModel'e bağlanacak.** InstrumentTrade `Create(Product product)` ile entity'yi doğrudan alıyor. Burada `OwnerUserId` veya `Visibility` gibi alanlar formdan sahte değerle gönderilebileceği için `ContactCreateViewModel` gibi sınıflar kullanılacak. Sahip bilgisi her zaman sunucuda `UserManager`'dan atanacak. (Areas/Customer'daki ViewModel kullanımıyla aynı mantık.)
5. **Silme işlemi POST + AntiForgery ile yapılacak ve kayıt gerçekten silinmeyecek.** InstrumentTrade'de silme bir GET linki. Burada `[HttpPost, ValidateAntiForgeryToken]` olacak ve kayıt sadece `IsDeleted = true` ile işaretlenecek (CRM geçmişi kaybolmasın, kural 6).
6. **Login'de `returnUrl` için `LocalRedirect` kullanılacak.** InstrumentTrade'deki `Redirect(returnUrl)` dış sitelere yönlendirmeye açık. Ayrıca pasif yapılan kullanıcının açık oturumu, `UpdateSecurityStampAsync` ile kapatılacak.
7. **Seed şifresi kodda olmayacak.** Geliştirme admin şifresi user-secrets'tan okunacak, seed sadece Development ortamında çalışacak.
8. **Durum alanları `string` yerine `enum` olacak.** InstrumentTrade'de `Order.Status = "Hazırlanıyor"` gibi metinler var. Burada `PropertyStatus.Active` gibi enum'lar kullanılacak (DB'de int). Yazım hatası riskini kaldırır, sıradan bir C# özelliği.
9. **Fotoğraflar `wwwroot` dışında saklanacak.** InstrumentTrade'de `ImageUrl` düz metin ve dosyalar herkese açık. "Sadece Ben" portföyün fotoğrafı URL ile açılmasın diye dosyalar `App_Data/uploads` altında tutulacak ve `PropertyPhotoController.Get(id)` ile, görünürlük kontrolünden sonra verilecek.
10. **Test projesi eklenecek** (`EmlakOfisi.Tests`, xUnit). InstrumentTrade'de test yok, ama CLAUDE.md her görev sonunda `dotnet test` istiyor. Paketler: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Microsoft.EntityFrameworkCore.InMemory`. InMemory paketinin gerekçesi: görünürlük sorgularını gerçek SQL Server olmadan test edebilmek.
11. **Kullanıcı bilgisini okumak için tek yardımcı.** `Extensions/UserAccessExtensions.cs` içinde `User.GetUserId()` ve `User.CanSeeAll()` (Admin veya Manager mı?) olacak. Böylece her controller bu kontrolü kendi başına, farklı farklı yazmayacak.
12. **Areas kullanılmayacak.** InstrumentTrade'de müşteri ve admin iki ayrı kullanıcı türü olduğu için `Areas/Customer` var. Burada herkes ofis çalışanı. Admin'e özel controller'lar, InstrumentTrade'deki gibi `Admin` önekiyle ayrılacak (`AdminUserController`, `RoleAssignController`).

Not: CLAUDE.md'deki "modüler monolit, tek solution" ifadesine uyuluyor: 4 proje tek solution'da ve tek uygulama olarak çalışıyor. Ek katman (CQRS, MediatR, AutoMapper, Unit of Work) **eklenmeyecek**.

---

## 1. Proje mimarisi ve klasör yapısı

```
EmlakOfisi.sln
├─ EmlakOfisi.EntityLayer/
│   ├─ Entities/   AppUser, AppRole, Contact, Property, PropertyPhoto, CustomerRequest,
│   │              Appointment, Activity, Transaction
│   └─ Enums/      Visibility, ListingType, PropertyType, PropertyStatus, ActivityType,
│                  AppointmentType, AppointmentStatus, RequestStatus, TransactionStatus
├─ EmlakOfisi.DataAccessLayer/
│   ├─ Abstract/      IGenericDal, IContactDal, IPropertyDal, ...
│   ├─ Concrete/      EfContactDal, EfPropertyDal, ...
│   ├─ Context/       AppDbContext
│   ├─ Repositories/  GenericRepository
│   └─ Migrations/
├─ EmlakOfisi.BusinessLayer/
│   ├─ Abstract/  IGenericService, IContactService, IPropertyService, ...
│   └─ Concrete/  ContactManager, PropertyManager, ...
├─ EmlakOfisi.WebUI/
│   ├─ Controllers/     Dashboard, Login, Contact, Property, PropertyPhoto, Activity,
│   │                   Appointment, AdminUser, Role, RoleAssign
│   ├─ Models/          *ViewModel
│   ├─ ViewComponents/Layout/  LayoutSidebarViewComponent, LayoutNavbarViewComponent
│   ├─ Extensions/      UserAccessExtensions
│   ├─ SeedData/        IdentityInitializer, TestDataSeeder (sadece Development)
│   ├─ Views/Shared/    _Layout.cshtml, Components/..., _ContactCard.cshtml (partial)
│   └─ wwwroot/         lib/tabler, css/site.css, js/site.js, manifest.json, sw.js, icons/
└─ EmlakOfisi.Tests/    ContactVisibilityTests, PropertyVisibilityTests, ...
```
Bağımlılık yönü InstrumentTrade ile aynı: WebUI → BusinessLayer → DataAccessLayer → EntityLayer. Tests → BusinessLayer + DataAccessLayer.

Teknoloji: **.NET 9 ile başla, .NET 10 LTS'ye yükselt (açık karar 1)**, EF Core (SDK sürümüyle aynı) + SqlServer, Identity.EntityFrameworkCore. Ekstra NuGet paketi yok.

---

## 2. Entity modelleri ve ilişkiler

Ortak alanlar (Contact ve Property): `Visibility Visibility = Visibility.OnlyMe`, `int CreatedByUserId`, `AppUser CreatedByUser`, `DateTime CreatedAt`, `DateTime? UpdatedAt`, `bool IsDeleted`.

**AppUser** : IdentityUser<int> → Name(50), Surname(50), IsActive (InstrumentTrade ile aynı).

**Contact** (Müşteri/Kişi)
- ContactId, Name(50, zorunlu), Surname(50, zorunlu), Phone(20, zorunlu), PhoneNormalized(15, son 10 hane, arama ve tekrar kontrolü için), Phone2?(20), Email?(150), Notes?(2000)
- Roller: `IsBuyer`, `IsSeller`, `IsTenant`, `IsLandlord` (bool). Bir kişi birden fazla role sahip olabilir ve kayıt tek kalır. Ayrı bir rol tablosuna gerek yok, dört sabit rol için bu en basit çözüm.
- İndeksler: `PhoneNormalized`, `(CreatedByUserId, Visibility)`, `(Surname, Name)`
- Tekrar kaydı engelleme: Kayıt eklerken aynı `PhoneNormalized` aranır. Kayıt kullanıcıya görünürse "Bu kişi zaten kayıtlı" uyarısı ve kayda link gösterilir. Görünmezse sadece "Bu numara ofiste kayıtlı, yöneticinize danışın" yazar, kişinin bilgisi gösterilmez. Unique constraint şimdilik konmayacak.

**Property** (Portföy)
- PropertyId, Title(200, zorunlu), ListingType (Sale/Rent), PropertyType (Residence/Land/Commercial/Other), Status (Active/Passive/Sold/Rented)
- Price `decimal(18,2)`, City(50), District(50), Neighborhood?(100), Address?(500), Latitude?/Longitude? `decimal(9,6)`
- GrossArea?/NetArea? int, RoomCount?(10, "3+1"), Floor?, BuildingAge?, Description?(4000)
- OwnerContactId? → Contact (mülk sahibi), AgentUserId → AppUser (sorumlu danışman), CreatedByUserId → AppUser
- İndeksler: `(Status, ListingType)`, `AgentUserId`, `(CreatedByUserId, Visibility)`, `City/District`
- Şehir/ilçe şimdilik düz metin. InstrumentTrade'deki City/Country tabloları burada MVP için fazla iş.

**PropertyPhoto**: PropertyPhotoId, PropertyId → Property, FileName(260), OriginalName(260), ContentType(50), SortOrder, IsCover, UploadedAt, UploadedByUserId.

**CustomerRequest** (Müşteri talebi): CustomerRequestId, ContactId → Contact, ListingType, PropertyType?, City?, District?, MinPrice?/MaxPrice? `decimal(18,2)`, RoomCount?, Notes?, Status (Open/Closed), CreatedByUserId, CreatedAt. Görünürlüğü bağlı olduğu Contact'tan gelir.

**Appointment** (Randevu/Gösterim): AppointmentId, Type (Meeting/Showing), StartAt, EndAt?, ContactId?, PropertyId?, AgentUserId, Location?(300), Notes?(2000), Status (Planned/Done/Cancelled), CreatedByUserId, CreatedAt. İndeks: `(AgentUserId, StartAt)`.

**Activity** (Görüşme ve işlem geçmişi): ActivityId, ContactId → Contact, PropertyId?, Type (Call/WhatsApp/Meeting/Showing/Offer/Note), Description(2000), ActivityDate, UserId → AppUser, CreatedAt. İndeks: `(ContactId, ActivityDate)`. Görünürlüğü Contact'tan gelir.

**Transaction** (Satış/Kiralama): TransactionId, PropertyId, CustomerContactId (alıcı/kiracı), Type (Sale/Rent), Status (Offer/Negotiation/Agreed/Completed/Cancelled), OfferPrice?, AgreedPrice?, CommissionRate? `decimal(5,2)`, CommissionAmount? `decimal(18,2)`, AgentUserId, CompletedAt?, Notes?, CreatedAt. Pazarlık adımları Activity (Type = Offer) olarak tutulur. Ayrı bir teklif tablosu sonraya kaldı.

**İlişki kuralları:** Tüm FK'larda `DeleteBehavior.Restrict` olacak. SQL Server'ın "multiple cascade paths" hatası da böylece oluşmaz, zaten silme işlemi `IsDeleted` ile yapılıyor. Kolon tipleri ve indeksler InstrumentTrade'deki gibi entity üzerinde DataAnnotation ile (`[Column(TypeName="decimal(18,2)")]`, `[Index(...)]`) tanımlanacak. `OnModelCreating`'e sadece Restrict ayarları yazılacak.

---

## 3. Roller, yetkiler ve kayıt görünürlüğü

Roller (`IdentityInitializer` ile oluşturulacak): **Admin**, **Manager** (Ofis Yöneticisi), **Agent** (Danışman).

| İşlem | Agent | Manager | Admin |
|---|---|---|---|
| Herkese Açık kaydı görme | ✓ | ✓ | ✓ |
| Başkasının "Sadece Ben" kaydını görme | ✗ | ✓ | ✓ |
| Düzenleme | Kendi oluşturduğu kayıt + sorumlusu olduğu portföy | Tümü | Tümü |
| Silme (IsDeleted) | Kendi oluşturduğu kayıt | Tümü | Tümü |
| Kullanıcı/rol yönetimi | ✗ | ✗ | ✓ |

- **Görünürlük kuralı (tek yerde, DAL sorgusunda):** `canSeeAll || Visibility == Public || CreatedByUserId == userId` (Property için `|| AgentUserId == userId` de eklenir). Her sorguda ayrıca `!IsDeleted` şartı vardır.
- **Düzenleme kuralı (Manager içinde):** `ContactManager.TCanEdit(contact, userId, canSeeAll)` ve `TCanDelete(...)`. Görünürlük ile düzenleme ayrı metotlar (kural 3).
- **Controller deseni:** `var c = _contactService.TGetVisibleById(id, User.GetUserId(), User.CanSeeAll()); if (c == null) return NotFound();`. Kayıt görünmüyorsa 403 değil 404 dönülür, böylece kaydın var olduğu bile anlaşılmaz. Düzenleme yetkisi yoksa `Forbid()` dönülür.
- **Bağlı kayıtlar** (Activity, CustomerRequest, PropertyPhoto) için önce bağlı oldukları Contact/Property'nin görünürlüğü kontrol edilir.
- **Arama, dashboard sayıları ve dropdown'lar** da aynı görünür-liste metotlarından gelir. Mülk sahibi gizli bir kişi ise portföy detayında adı gösterilmez, sadece "Gizli kayıt" yazar.
- Pasif kullanıcı giriş yapamaz (InstrumentTrade ile aynı), açık oturumu da security stamp ile düşer.
- Her yetki değişikliğinden sonra `yetki-denetcisi` agent'ı çalıştırılır.

---

## 4. Mobile-first arayüz yaklaşımı
- Tabler UI (**açık karar 2**). Statik dosyalar `wwwroot/lib/tabler` altına konur, npm kullanılmaz. Masaüstünde sol menü, mobilde üstte hamburger menü ve altta sabit 4 butonluk menü (Ana sayfa, Müşteri, Portföy, Randevu).
- Listeler mobilde kart (partial `_ContactCard`), `md` ve üstünde tablo olarak görünür. 375px genişlikte yatay kaydırma olmaz.
- Dokunmatik hedefler en az 44px. Formlar tek sütunlu, uygun input tipleriyle (`type="tel"`, `inputmode="numeric"`).
- Hızlı arama: liste sayfasının üstünde sabit arama kutusu, sunucu tarafında GET `?q=` ile arama (JS kütüphanesi yok).
- İletişim: `tel:` linki ve `https://wa.me/90XXXXXXXXXX` linki (PhoneNormalized'dan üretilir).
- Kameradan fotoğraf: `<input type="file" accept="image/*" capture="environment" multiple>`, sunucuda boyut ve uzantı kontrolü yapılır.
- Harita: koordinat varsa `https://www.google.com/maps?q=lat,lng`, yoksa adres ile arama linki. Harita kütüphanesi eklenmez.
- PWA: `manifest.json` ve basit `sw.js` (sadece kurulabilirlik ve statik dosya cache'i). Offline senkronizasyon yapılmaz.

---

## 5. MVP kapsamı
**MVP – Faz 1 (çalışan çekirdek):** Giriş/çıkış, kullanıcı ve rol yönetimi, müşteri CRUD + arama + roller + tel/WhatsApp, görüşme notları (Activity), portföy CRUD + durum + mülk sahibi + danışman + arama + harita linki, fotoğraf yükleme (kamera), basit randevu listesi/ekleme, dashboard sayıları, PWA kurulumu.
**MVP – Faz 2:** Müşteri talepleri (CustomerRequest), satış/kiralama işlemleri ve komisyon hesabı, dashboard'da satış/kira ve komisyon özetleri.
**Sonraya:** Talep–portföy otomatik eşleştirme, belge yükleme (tapu vb.), takvim görünümü, hatırlatma bildirimleri (push/e-posta), ayrı teklif geçmişi tablosu, raporlar/Excel, değişiklik kaydı (audit log), il/ilçe tabloları, harita gömme, offline çalışma, muhasebe ve e-fatura.

---

## 6. Geliştirme görevleri (her biri: build + test + commit önerisi)
1. `altyapi: solution ve katman projeleri` – 4 proje + Tests, referanslar, boş build/test yeşil.
2. `entity: identity ve temel entityler` – AppUser/AppRole, enum'lar, tüm entity'ler.
3. `dal: dbcontext, generic repository, user-secrets` – AppDbContext(options), GenericRepository, Program.cs kaydı, `InitialCreate` migration'ı (yeni veritabanı; `database update` onayla çalıştırılır).
4. `kimlik: giris, cikis, rol seed` – LoginController, global Authorize, IdentityInitializer, cookie ayarları.
5. `arayuz: tabler layout ve menuler` – _Layout, Sidebar/Navbar ViewComponent'leri, mobil alt menü, boş Dashboard.
6. `yonetim: kullanici ve rol atama` – AdminUser (oluştur/aktif-pasif), Role, RoleAssign.
6b. `altyapi: net10 yukseltme` – TargetFramework ve paket sürümlerini 10'a çek, build/test yeşil (.NET 10 SDK kurulu olmalı).
7. `musteri: gorunurluk sorgulari ve testleri` – IContactDal/EfContactDal/ContactManager + 6 test (3 rol × 2 görünürlük) + düzenleme/silme testleri. → yetki-denetcisi
8. `musteri: liste ve hizli arama` – **ilk ekran** (aşağıda) + test verisi seed'i.
9. `musteri: ekle, guncelle, detay, sil` – ViewModel'ler, telefon tekrar kontrolü, POST silme. → yetki-denetcisi
10. `musteri: gorusme gecmisi` – müşteri detayında Activity ekleme ve listeleme. → yetki-denetcisi
11. `portfoy: gorunurluk sorgulari ve testleri` → yetki-denetcisi
12. `portfoy: liste, arama, CRUD, harita linki` → yetki-denetcisi
13. `portfoy: fotograf yukleme ve guvenli gosterim` → yetki-denetcisi
14. `randevu: liste ve ekleme`
15. `dashboard: sayilar`
16. `pwa: manifest ve service worker`
16b. (MVP, formlar yapıldıktan sonra) `arayuz: not alanlari icin bas-konus mikrofon dugmesi`
   - Web Speech API, dil `tr-TR`, harici paket/servis yok.
   - Düğme basılı tutulunca dinler, bırakınca durur, metni alanın sonuna ekler.
   - Tek ortak JS dosyası; sadece `data-mic` işaretli **textarea**'larda çalışır.
   - Sadece serbest metin alanları: görüşme/aktivite notları, randevu notu, müşteri talep açıklaması (görev 17 gelince), portföy açıklaması.
   - Telefon, fiyat, tarih gibi yapılandırılmış alanlara eklenmez.
   - Tarayıcı desteklemiyorsa düğme görünmez.
   - Dokunmatik için büyük düğme (en az 44px), dinlerken görsel geri bildirim.
   - Not: Chrome ve Safari konuşmayı tanımak için sesi kendi sunucularına (Google/Apple) gönderir; uygulama ek servis kullanmaz ama ses tarayıcı üreticisine gider. HTTPS gerekir.
17. (Faz 2) `talep: musteri talepleri` → 18. `islem: satis/kiralama ve komisyon` → 18b. `islem: danisman primi` (tamamlandı)

### Eklenen özellikler (2026-11-07, kullanıcı onayıyla)
Sıra: önce yayın hazırlığı (Türkçe form mesajları, canlı sistemde ilk admin), sonra kontrat dosyası (22–23, MVP'ye dahil, yayından önce), sonra aday listesi (19–21, Faz 3). .NET 10'a geçiş şimdilik ertelendi (kullanıcı kararı). Her görevin sonunda `yetki-denetcisi` ve `kod-inceleyici` çalışır.

**Aday (potansiyel müşteri) arama listesi – Faz 3**
- Ayrı tablo yok. `Contacts`'a eklenecekler:
  - `Stage` (enum `ContactStage`: Aday=1, Müşteri=2; mevcut kayıtlar Müşteri olur)
  - `LeadSource` (enum: Sahibinden, Emlakjet, Hepsiemlak, Tabela, Referans, SosyalMedya, Diğer)
  - Kısa mülk bilgisi: `LeadDistrict` (semt), `LeadPropertyType`, `LeadListingUrl`
  - `LastCallResult` (enum `CallResult`), `NextCallAt`, `DoNotCall` (bool)
  - `SharedWithUserId` (görünürlük "Seçili danışman" için)
  - İndeks: (`CreatedByUserId`, `Stage`, `NextCallAt`)
- `Activities`'e `CallResult` (enum, boş olabilir). Her arama `Type = Call` olan bir görüşme kaydıdır; ayrı arama tablosu yok.
- `CallResult`: Ulaşılamadı, Tekrar ara, İlgilenmiyor, Görüşme ayarlandı, Aranmak istemiyor.
- **Görünürlük (müşteri ve aday için):** Sadece Ben (varsayılan) / Herkese Açık / **Seçili danışman** (ekleyen + seçilen tek danışman). Yöneticiler (Admin ve Ofis yöneticisi) hepsini görür. Seçili danışman kaydı görür ve arama sonucu girer; kaydı düzenleyemez, silemez. Kural `VisibilityRules`'ta tek yerde.
- **"Aranacaklar" listesi:** Danışmanın aranmayı bekleyen tüm adayları (kendi + kendisine paylaşılan). Sıralama: tarihi geçmiş/bugün olanlar (vurgulu), sonra ileri tarihliler, en sonda tarihsiz. Filtre: bugün/bu hafta, kaynak, son sonuç. Mobilde dokun-ara, dönüşte tek dokunuşla sonuç. "Tekrar ara" sonraki tarihi sorar. Ana ekranda "Bugün aranacak: N".
- "Aranmak istemiyor" olanlar ve müşteriye çevrilenler listeye düşmez.
- **Telefon tekrar kontrolü:**
  - Adaylarda engel yerine uyarı + "Yine de kaydet". Uyarı numaranın kimde olduğunu söylemez ("Başka bir danışmanın listesinde kayıtlı. Kimde olduğunu yöneticiniz görebilir"). Görebildiği kayıtsa link verilir.
  - "Aranmak istemiyor" numaralarda bu ayrıca yazılır; yeni kayıt da "aranmak istemiyor" işaretli açılır.
  - Müşterilerde mevcut engel kalır. Müşteriye çevirirken aynı numarayla müşteri varsa çevirme durur.
  - Telefon zaten normalize edilip (`PhoneNormalized`, son 10 hane) indeksli saklanıyor (görev 7).
- **"Müşteriye çevir":** aynı kaydın aşaması değişir, rol seçilir (alıcı/satıcı/kiracı/mülk sahibi), geçmiş korunur.

**Kontrat dosyası (sade) – MVP, yayından önce**
- `Transactions`'a eklenecekler:
  - `LeaseStartDate`, `LeaseEndDate` (sadece kiralama; bitiş başlangıçtan sonra)
  - `ContractFileName` (GUID), `ContractOriginalName`, `ContractContentType`, `ContractUploadedAt`, `ContractUploadedByUserId`
  - İndeks: (`Type`, `Status`, `LeaseEndDate`)
- İşlem başına tek dosya; yenisi eskisinin yerine geçer. Genel Documents tablosu ve belge türleri şimdilik yok.
- PDF/JPG/PNG, en fazla 10 MB; tür içerikten (imza baytları) kontrol edilir.
- `wwwroot` dışında, `Documents:Root` ayarındaki klasörde (varsayılan `App_Data/documents`) GUID adla saklanır.
- Yükleme: işlemi düzenleyebilen ("aynı gün" kuralı dahil). İndirme: sadece controller üzerinden, işlemin görünürlük kuralıyla; görmeyen 404; önbellekte tutulmaz.
- Mobilde dosya alanında `capture` yok: kamera, dosyalar ve tarayıcı (taranmış PDF) birlikte sunulur.
- Ana ekranda "önümüzdeki 60 gün içinde biten kira kontratları" (danışman kendi işlemleri, yönetici ofis).
- Yayın kontrol listesine: fotoğraf ve belge klasörleri yedeklenmeli, IIS uygulama havuzunun yazma izni olmalı.

**Görevler**
19. (Faz 3) `musteri: aday asamasi, secili danisman gorunurlugu, telefon uyarisi` → yetki-denetcisi
20. (Faz 3) `aday: aranacaklar listesi ve arama sonucu` → yetki-denetcisi
21. (Faz 3) `aday: musteriye cevir` → yetki-denetcisi
22. (MVP) `islem: kira tarihleri ve kontrat dosyasi` → yetki-denetcisi
23. (MVP) `dashboard: biten kira kontratlari` → yetki-denetcisi

---

## 7. İlk geliştirilecek ekran: Müşteri Listesi ve Hızlı Arama (`/Contact`)
Danışmanların sahada en çok kullanacağı ekran bu olduğu için ve görünürlük kuralını ilk kez uçtan uca burada test edeceğimiz için ilk ekran olarak seçildi.

**Kabul kriterleri**
1. Giriş yapmamış kullanıcı `/Contact` adresine girdiğinde `/Login` sayfasına yönlenir.
2. Agent rolündeki kullanıcı, kendi oluşturduğu kayıtları ve "Herkese Açık" kayıtları görür. Başka bir danışmanın "Sadece Ben" kaydı listede ve arama sonuçlarında **görünmez**.
3. Manager ve Admin tüm kayıtları görür. Listede her kaydın yanında görünürlük rozeti (Sadece Ben / Herkese Açık) bulunur.
4. Görünmeyen bir kaydın id'si ile `/Contact/Detail/{id}` açılmaya çalışılırsa **404** döner (sunucu tarafı kontrol).
5. Arama ad, soyad ve telefonda (son haneler dahil, boşluk ve 0 ile 90 öneki fark etmez) çalışır. Arama sunucuda, görünürlük filtresiyle **aynı sorguda** yapılır.
6. Rol filtresi (Alıcı / Satıcı / Kiracı / Mülk sahibi) seçilebilir, birden fazla rolü olan kişi her filtrede çıkar.
7. Silinmiş (`IsDeleted`) kayıtlar listelenmez. Sayfalama 20 kayıt.
8. 375px genişlikte yatay kaydırma yoktur, kayıtlar kart olarak görünür. Ara ve WhatsApp butonları en az 44px'tir, `tel:` ve `wa.me` linkleri doğru numarayı açar.
9. Geliştirme seed'inde sahte veri bulunur: 1 Admin, 1 Manager, 2 Agent ve her danışman için "Sadece Ben" ve "Herkese Açık" örnek kişiler (gerçek veri yok).
10. `dotnet build` ve `dotnet test` yeşil, görünürlük testleri geçer, `yetki-denetcisi` raporunda açık bulgu yoktur.

---

## Açık kararlar (onayda belirtin)
1. **.NET sürümü:** Kurulu olan **.NET 9** ile başlanacak, sonra **.NET 10 LTS**'ye geçilecek. Yükseltme küçük bir iş: her `.csproj` dosyasında `net9.0` → `net10.0`, EF Core ve Identity paketlerinde `9.x` → `10.x`, ardından build ve test. Bu aşamadaki kod (MVC, EF Core, Identity) iki sürümde de aynı yazılıyor. Tek dikkat edilmesi gereken: .NET 9'un desteği **10 Kasım 2026**'da bitiyor. Bu yüzden yükseltme, uygulama gerçek kullanıma açılmadan önce yapılmalı. Görev listesinde **görev 6'dan sonra ayrı bir görev** olarak yer alacak (`altyapi: net10 yukseltme`). O zamana kadar .NET 10 SDK'sının kurulmuş olması gerekiyor. Aynı solution'ın sürüm geçişi olduğu için migration'lar etkilenmez.
2. **UI şablonu:** Öneri **Tabler UI** (CLAUDE.md ile uyumlu). Alternatif: InstrumentTrade'deki Modernize şablonu.
3. Onaydan sonra bu kararlar NEXT_STEPS.md "Açık kararlar" bölümüne yazılacak ve görev 1 ile başlanacak.

## Doğrulama
- Her görevin sonunda `test-calistirici` ile `dotnet build` + `dotnet test` çalıştırılır.
- Görünürlük: Tests projesinde InMemory DB ile rol × görünürlük matrisi test edilir. Ayrıca uygulama tarayıcıda farklı seed kullanıcılarıyla açılıp liste, arama ve doğrudan URL denemeleri yapılır.
- Mobil: Tarayıcı panelinde 375px görünümde ekran görüntüsüyle kontrol edilir.
- Commit öncesi `kod-inceleyici` çalıştırılır.
