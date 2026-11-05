# NEXT_STEPS

## Tamamlananlar
- 2026-11-05: İlk görev (mimari, entity modeli, roller/görünürlük, mobil yaklaşım, MVP, görev listesi, ilk ekran kabul kriterleri) hazırlandı ve onaylandı → `docs/MIMARI_PLAN.md`
- 2026-11-05: Görev 1 `altyapi: solution ve katman projeleri` – EntityLayer, DataAccessLayer, BusinessLayer, WebUI (MVC) ve Tests (xUnit) projeleri, net9.0. Referanslar WebUI → BL → DAL → Entity, Tests → BL + DAL. Build ve test yeşil (1 iskelet testi).

- 2026-11-05: Görev 2 `entity: identity ve temel entityler` – AppUser/AppRole, 9 enum (sabit int değerli), Contact, Property, PropertyPhoto, CustomerRequest, Appointment, Activity, Transaction. EntityLayer'a `Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.0.22` eklendi. Varsayılan değer testleri (OnlyMe, Active, IsActive) dahil 5/5 test yeşil.

## Sıradaki küçük görev
- Görev 3 `dal: dbcontext, generic repository, user-secrets` – `AppDbContext(DbContextOptions)` (OnConfiguring yok), DbSet'ler, OnModelCreating'de tüm FK'lar `DeleteBehavior.Restrict`, `IGenericDal`/`GenericRepository`, DAL'a EF Core SqlServer + Design/Tools paketleri (9.0.x), WebUI'de user-secrets ile `DefaultConnection`, `InitialCreate` migration'ı (sadece oluşturulur; `database update` onayla). `AssemblyMarker.cs` dosyaları ve `InternalsVisibleTo` kaldırılır.

## Açık kararlar / notlar
- Mimari, InstrumentTrade projesindeki 4 katmanlı yapıya göre kuruluyor (Generic Dal/Service, EfXDal, XManager). Farkları `docs/MIMARI_PLAN.md` bölüm 0'da anlatılıyor.
- .NET 9 ile başlandı. **Görev 6b'de .NET 10 LTS'ye yükseltilecek** (.NET 9'un desteği 10 Kasım 2026'da bitiyor), o zamana kadar .NET 10 SDK kurulmalı.
- UI: Tabler UI (görev 5'te `wwwroot/lib/tabler` altına eklenecek, şablonun getirdiği varsayılan Bootstrap/Home sayfaları o zaman temizlenecek).
- `AssemblyMarker.cs` (DAL ve BL) geçici; görev 3'te gerçek sınıflar gelince silinecek.
- Tarih alanları InstrumentTrade'deki gibi `DateTime.Now` kullanıyor (tek saat dilimi, Türkiye). Çok bölgeli kullanım gerekirse UTC'ye geçilir.
- AppUser'da ters yönde koleksiyon navigation'ı yok, bilerek eklenmedi: aynı tabloya birden çok FK olduğunda EF'in ilişkileri karıştırmaması için.
- `.gitignore`: fotoğraflar `App_Data/uploads` altında saklanacak, görev 13'te bu klasör eklenecek.
