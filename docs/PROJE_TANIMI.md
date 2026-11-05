# Proje: Emlak Ofisi Yönetim Sistemi (ERP / CRM)

Sen kıdemli bir .NET Software Architect ve Full Stack Developer olarak görev yapacaksın.

Bir emlak ofisinin günlük operasyonlarını dijital ortamda yönetebileceği, modern ve mobil uyumlu bir ERP/CRM uygulaması geliştireceğiz.

**Önemli:** Bu bir emlak ilan sitesi değildir. Emlak danışmanlarının, ofis yöneticilerinin ve çalışanlarının kullanacağı profesyonel bir yönetim yazılımıdır.

## 1. Teknoloji ve Mimari

- Backend: ASP.NET Core (güncel LTS sürümü)
- Frontend: ASP.NET Core MVC + Razor Views
- UI: Tabler UI + Bootstrap 5
- ORM: Entity Framework Core
- Veritabanı: Microsoft SQL Server
- Authentication: ASP.NET Core Identity
- Mimari: Modüler Monolit
- Mobil: Mobile-First Responsive Design + PWA
- Versiyon Kontrolü: Git

Clean Code, SOLID ve güvenli yazılım geliştirme prensiplerine uy. Ancak gereksiz katmanlar ve aşırı mühendislik uygulama.

## 2. Temel Modüller

### Dashboard
- Aktif portföy sayısı
- Toplam müşteri sayısı
- Yaklaşan randevular
- Satış ve kiralama özetleri
- Komisyon gelirleri

### Müşteri Yönetimi (CRM)
- Müşteri ekleme, güncelleme ve arama
- Alıcı, satıcı, kiracı ve mülk sahibi rolleri
- Telefon ve WhatsApp üzerinden hızlı iletişim
- Müşteri talepleri
- Görüşme ve işlem geçmişi

Bir kişi birden fazla role sahip olabilir. Aynı kişi için gereksiz tekrar kayıt oluşturma.

### Portföy Yönetimi
- Satılık ve kiralık gayrimenkuller
- Konut, arsa, iş yeri gibi gayrimenkul türleri
- Mülk sahibi ilişkisi
- Adres, konum, fiyat, özellik ve açıklamalar
- Fotoğraf ve belge yükleme
- Aktif, pasif, satıldı, kiralandı durumları
- Portföy sorumlusu danışman

### Görüşme ve Randevu Yönetimi
- Müşteri görüşmeleri
- Gayrimenkul gösterimleri
- Randevu tarihi ve saati
- Görüşme notları
- Hatırlatma ve takip kayıtları

### Satış / Kiralama Yönetimi
- Müşteri-portföy eşleştirmesi
- Teklif ve pazarlık takibi
- Satış/kiralama işlemleri
- Komisyon hesaplamaları
- İşlem geçmişi

İlk sürümde kapsamlı muhasebe veya e-fatura entegrasyonu geliştirme.

## 3. Kayıt Bazlı Görünürlük

Müşteri ve portföy kayıtlarında iki seçenek bulunacak:

**Herkese Açık:** Ofisteki yetkili kullanıcılar kaydı görebilir.

**Sadece Ben:** Kaydı oluşturan kullanıcı ve yetkili yöneticiler görebilir.

Görünürlük ile düzenleme/silme yetkilerini birbirinden ayır.

Güvenliği yalnızca frontend üzerinde uygulama. Listeleme, arama, detay, güncelleme, silme ve API erişimlerinde sunucu tarafında yetki kontrolü yap.

Varsayılan görünürlük "Sadece Ben" olsun.

## 4. Mobil Kullanım

Emlak danışmanları sahada telefon üzerinden çalışacak.

Bu nedenle:
- Mobile-first yaklaşımı kullanılmalı.
- Menü ve formlar dokunmatik kullanım için optimize edilmeli.
- Mobilde hızlı müşteri ve portföy arama bulunmalı.
- Telefon ve WhatsApp bağlantıları sunulmalı.
- Telefon kamerasından fotoğraf yüklenebilmeli.
- Portföy konumu haritada açılabilmeli.
- PWA desteğiyle ana ekrana eklenebilmeli.

İlk sürümde internet bağlantısı zorunlu olabilir. Offline senkronizasyon şimdilik geliştirilmesin.

## 5. İlk Veritabanı Modeli

Aşağıdaki tabloları başlangıç modeli olarak değerlendir:

- Contacts
- Properties
- PropertyPhotos
- CustomerRequests
- Appointments
- Activities
- Transactions
- ASP.NET Core Identity kullanıcı tabloları

Entity ilişkilerini, foreign key yapılarını, indeksleri, veri tiplerini ve zorunlu alanları tasarla.

Müşteri ve portföy kayıtları kullanıcıyla ilişkilendirilmeli. Para alanlarında decimal kullanılmalı. Kullanıcı rolleri ve kayıt bazlı erişim kuralları dikkate alınmalı.

Veritabanı modelini geliştirirken gerekli gördüğün değişiklikleri gerekçeleriyle öner.

## 6. Geliştirme Kuralları

1. Projeyi küçük, bağımsız ve test edilebilir görevlere ayır.
2. Her görev sonunda build ve uygun testleri çalıştır.
3. Mevcut çalışan özellikleri bozma.
4. Gereksiz NuGet paketleri veya mimari katmanlar ekleme.
5. Kod değişikliklerini açıklayarak ilerle.
6. Veritabanında veri kaybına neden olabilecek işlemleri onaysız gerçekleştirme.
7. Connection string, şifre, API key veya gizli bilgileri kaynak kodda tutma. Güvenli yapılandırma kullan.
8. Projeyi Git ile yönet; küçük ve anlamlı commit'ler öner.
9. Gerçek müşteri verileriyle geliştirme yapma. Test verileri kullan.
10. Her oturum sonunda tamamlanan işleri ve bir sonraki küçük görevi `NEXT_STEPS.md` dosyasında takip et.

## 7. İlk Görevin

Henüz kod yazma veya dosya oluşturma.

Önce aşağıdaki çıktıları hazırla:

1. Önerdiğin proje mimarisi ve klasör yapısı.
2. Entity modelleri ve veritabanı ilişkileri.
3. Kullanıcı rolleri, yetkiler ve kayıt görünürlüğü tasarımı.
4. Mobile-first arayüz yaklaşımı.
5. MVP kapsamı ve sonraya bırakılacak özellikler.
6. Küçük parçalara ayrılmış geliştirme görevleri.
7. İlk geliştirilecek ekran ve kabul kriterleri.

Önceliğimiz önce çalışan bir MVP çıkarmak, ardından sistemi aşamalı geliştirmek.

**Önerilerini sunduktan sonra onayımı bekle. Henüz kodlama aşamasına geçme.**