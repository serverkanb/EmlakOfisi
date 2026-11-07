# Emlak Ofisi Yönetim Sistemi (ERP / CRM)

Emlak danışmanları ve ofis yöneticileri için iç yönetim yazılımı. **İlan sitesi değildir.**
Detaylı gereksinimler: @docs/PROJE_TANIMI.md
Oturum takibi: @NEXT_STEPS.md

## Teknoloji
- ASP.NET Core (güncel LTS), MVC + Razor Views
- Tabler UI + Bootstrap 5, mobile-first, PWA
- EF Core + Microsoft SQL Server
- ASP.NET Core Identity
- Modüler monolit, tek solution. Gereksiz katman/proje açma.

## Ölçek
- Uygulama yaklaşık 10 kullanıcılı tek bir emlak ofisi için.
- Windows paylaşımlı hosting (IIS + MSSQL) üzerinde çalışacak.
- Cache, kuyruk, arka plan servisi, harici depolama gibi büyük ölçek çözümleri önerilmez.
- Telefon fotoğrafları yüklenirken küçültülür.

## Mutlak kurallar
1. **Görünürlük güvenliği sunucu tarafındadır.** Listeleme, arama, detay, güncelleme, silme ve API uçlarının hepsinde yetki kontrolü yapılır. Sadece frontend'de gizlemek yetki kontrolü sayılmaz.
2. Müşteri/portföy kayıtlarında varsayılan görünürlük **"Sadece Ben"**dir.
3. Görünürlük (kim görebilir) ile düzenleme/silme yetkisi ayrı kurallardır.
4. Para alanları `decimal` (SQL'de `decimal(18,2)`).
5. Connection string, şifre, API key kaynak kodda olmaz. Geliştirmede `dotnet user-secrets` kullanılır. `appsettings.json` içine sır yazılmaz.
6. Veri kaybına yol açabilecek migration veya SQL (DROP, kolon silme, tip daraltma) **onay almadan çalıştırılmaz**.
7. Gerçek müşteri verisi kullanılmaz, sadece seed/test verisi kullanılır.
8. Gereksiz NuGet paketi eklenmez. Yeni paket gerekiyorsa önce gerekçesi sunulur.

## Çalışma şekli
- İşler küçük, bağımsız ve test edilebilir görevlere bölünür.
- Kod değişikliklerinden önce plan sunulur, yapılan değişiklikler kısaca açıklanır.
- Her görev sonunda `dotnet build` ve `dotnet test` çalıştırılır. Kırmızı test varken görev bitmiş sayılmaz.
- Mevcut çalışan özellikler bozulmaz.
- Her görev sonunda küçük ve anlamlı bir commit mesajı önerilir. Format: `modul: kısa açıklama`
- Oturum sonunda `NEXT_STEPS.md` güncellenir: tamamlananlar ve bir sonraki küçük görev.
- Kullanıcı ile Türkçe iletişim kurulur. Kod, sınıf ve tablo adları İngilizce olur.

## Agent'lar
- `yetki-denetcisi`: Kayıt erişimine dokunan her değişiklikten sonra çalıştırılır.
- `test-calistirici`: Build ve test için kullanılır, sadece hataları özetler.
- `kod-inceleyici`: Commit öncesi inceleme yapar.
