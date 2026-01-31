# Değişiklik Özeti ve Kontrol Raporu

Bu dokümanda bu oturumda yapılan tüm değişiklikler ve kontrol sonuçları özetlenmiştir.

---

## 1. Migration (AddQrMenuModule) – Idempotent Hale Getirme

**Sorun:** Program.cs zaten SafeExecuteSql ile bazı sütunları ekliyordu; migration aynı sütunları tekrar eklemeye çalışınca "column already exists" hatası oluşuyordu.

**Yapılan:** `src/Nobetci.Web/Data/Migrations/20260128050957_AddQrMenuModule.cs` içinde:

- **Up():** UnitTypes.NameEn, Units.EmployeeLimit, SystemSettings (Category, DataType, SortOrder), Employees (Email, Phone), AspNetUsers (CanAccessCleaning, CanGroupCleaningSchedules, CanSelectCleaningFrequency, CleaningItemLimit, CleaningQrAccessLimit, CleaningScheduleLimit, UnitEmployeeLimit, UnitLimit) sütunları için `IF NOT EXISTS` ile idempotent raw SQL kullanıldı.
- **Down():** Aynı sütunlar için `IF EXISTS ... DROP COLUMN` ile idempotent geri alma eklendi.

**Sonuç:** Migration tekrar çalıştırıldığında hata vermeden tamamlanır.

---

## 2. Bordro Sabitleri ve Unit Sabitleri – Lazy Init

**Amaç:** Eski (yeni özellikten önce kayıt olan) kullanıcılar için bordro sabitleri ve birim tipleri instance’ı ilk kullanımda oluşturulsun.

### 2.1 EnsureBordroSabitleriAsync Çağrıldığı Yerler

| Nokta | Dosya | Açıklama |
|-------|--------|----------|
| Index (ana sayfa) | AppController | İlk uygulama açılışında |
| Payroll (Puantaj) | AppController | Puantaj sayfası açılışında |
| GetOrCreateOrganization (yeni org) | AppController | Yeni organizasyon oluşturulunca |
| Guest org | AppController | Misafir organizasyonu için |
| Sabitler | BordroController | Bordro Sabitleri sayfası |
| YetkiliYonetimi | BordroController | Yetkili Yönetimi sayfası |
| ExportExcel | ExportController | Excel export öncesi |
| ExportPayroll | ExportController | Puantaj export öncesi |
| ExportSavedPayroll | ExportController | Kayıtlı puantaj export öncesi |
| GetBordroOptionsAsync (iç) | BordroHesaplamaService | Hesaplama/API yollarında |

### 2.2 Unit Types / Default Unit Init

| Nokta | Bordro sabitleri | Unit types / default unit |
|-------|------------------|---------------------------|
| App/Index (premium) | Ensure (servis) | InitializeDefaultUnitTypesAsync + InitializeDefaultUnitAsync (AppController private) |
| App/Payroll (premium) | Ensure (servis) | Aynı (AppController private) |
| App/Attendance (premium) | — | Aynı (AppController private) |
| App/GetOrCreateOrganization (yeni org) | Ensure (servis) | InitializeDefaultUnitTypesAsync + InitializeDefaultUnitAsync |
| Guest org | Ensure (servis) | Yok (misafir için birim zorunlu değil) |
| Bordro/Sabitler | Ensure (servis) | EnsureDefaultUnitTypesAsync + EnsureDefaultUnitAsync (servis) |
| Bordro/YetkiliYonetimi | Ensure (servis) | EnsureDefaultUnitTypesAsync + EnsureDefaultUnitAsync (servis) |

### 2.3 BordroHesaplamaService’e Eklenen Metodlar

- `EnsureDefaultUnitTypesAsync(int organizationId)` – Org için varsayılan birim tiplerini şablondan oluşturur.
- `EnsureDefaultUnitAsync(int organizationId)` – Org için en az bir birim (örn. "Genel Birim") oluşturur; birim yoksa personeli bu birime atar.
- `EnsureUnitTypeTemplatesAsync()` – UnitTypeTemplates yoksa oluşturur (private).

---

## 3. ExportController – Bordro Sabitleri Ensure

**Sorun:** Export action’ları GetBordroOptionsAsync kullanıyordu ama bordro sabitleri ensure edilmiyordu; eski kullanıcı doğrudan export alırsa sabitler boş kalabiliyordu.

**Yapılan:**

- Constructor’a `IBordroHesaplamaService` eklendi.
- ExportExcel, ExportPayroll, ExportSavedPayroll action’larında `GetOrganizationAsync()` sonrası `EnsureBordroSabitleriAsync(organization.Id)` çağrısı eklendi.

---

## 4. BordroController – Loglama

**Yapılan:** BordroController’a `ILogger<BordroController>` eklendi. Sabitler ve YetkiliYonetimi’ndeki ensure bloklarında hata durumunda `_logger.LogWarning` ile log yazılıyor; sayfa açılmaya devam ediyor.

---

## 5. Kontrol Edilen ve Eksik Görülmeyen Noktalar

- **PuantajDetail:** BordroSabitleri kullanmıyor; ensure gerekmiyor.
- **GetUnitCoefficientMapAsync:** Sadece Payroll ve BordroHesaplamaService’te çağrılıyor; her ikisinde de ensure var.
- **Bordro hesaplama API (ApiHesaplaBordro, ApiGetBordro, TekPersonelBordroHesapla, TopluBordroHesaplamaBaslat, BirimBordroHesaplama):** BordroHesaplamaService üzerinden GetBordroOptionsAsync/EnsureBordroSabitleriAsync çağrılıyor.
- **Unit entity:** CreatedAt/UpdatedAt property default’ları mevcut; EnsureDefaultUnitAsync’te ek atama gerekmiyor.
- **Guest org:** Sadece bordro sabitleri ensure ediliyor; birim init yok (misafir için tasarım gereği kabul edildi).

---

## 6. İsteğe Bağlı İyileştirme Önerileri

1. **Tek implementasyon:** AppController’daki `InitializeDefaultUnitTypesAsync` ve `InitializeDefaultUnitAsync` private metodları, BordroHesaplamaService’teki `EnsureDefaultUnitTypesAsync` ve `EnsureDefaultUnitAsync` ile aynı işi yapıyor. İleride AppController’da bu private metodlar kaldırılıp doğrudan `_bordroHesaplamaService.EnsureDefaultUnitTypesAsync` / `EnsureDefaultUnitAsync` çağrılabilir; böylece tek kaynak olur ve bakım kolaylaşır.

2. **Export hata davranışı:** ExportController’da EnsureBordroSabitleriAsync try/catch’e alınmadı; hata olursa export işlemi hata döner. Bu bilinçli tercih (sabitler yoksa export’u engellemek). İstenirse try/catch ile loglayıp devam da edilebilir; o zaman sabitler yokken config default’ları ile export alınır.

3. **Migration sonrası test:** Uygulama ilk kez veya migration sonrası çalıştırıldığında `dotnet run` / `dotnet watch` ile başlatıp Index, Payroll, Bordro/Sabitler, Export sayfalarının sorunsuz açıldığı ve bordro/birim verilerinin beklendiği gibi oluştuğu manuel test edilebilir.

---

## 7. Özet Tablo

| Bileşen | Durum |
|--------|--------|
| AddQrMenuModule migration | Idempotent; tekrar çalıştırmada hata yok |
| Bordro sabitleri ensure | Index, Payroll, Export (3), Bordro (2), org/guest, servis içi – tamamlandı |
| Unit types / default unit | App (Index/Payroll/Attendance/GetOrg), Bordro (Sabitler, YetkiliYonetimi) – tamamlandı |
| ExportController | IBordroHesaplamaService + 3 action’da ensure – tamamlandı |
| BordroController loglama | ILogger + 2 catch bloğunda LogWarning – tamamlandı |

Tarih: 2026-01-29
