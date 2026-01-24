# 📋 Güncellemeler - 24 Ocak 2026

## 🎯 Özet
Bu güncelleme ile Hemşire Nöbet Sistemi modüler bir yapıya kavuştu. Birim yönetimi, personel düzenleme ve admin panel özellikleri genişletildi.

---

## 1. 👥 Personel Havuzu Modal'ı (Birimden Bağımsız)

### Değişiklikler
- Personeller modal'ı artık **tüm personelleri** gösteriyor (birim filtresinden bağımsız)
- Her personelin yanında **hızlı birime atama** özelliği (select box)
- Modal başlığı "Personel Yönetimi" → "Personel Havuzu" olarak değişti

### Teknik Detaylar
- `AppViewModel`'e `AllEmployees` property eklendi
- `AppController.Index`'te tüm personeller ayrıca yükleniyor
- `quickAssignUnit()` JavaScript fonksiyonu eklendi
- `.emp-unit-select` CSS stilleri eklendi

### Dosyalar
- `src/Nobetci.Web/Models/AppViewModel.cs`
- `src/Nobetci.Web/Controllers/AppController.cs`
- `src/Nobetci.Web/Views/App/Index.cshtml`

---

## 2. ✏️ Personel Düzenleme Özelliği

### Yeni Özellikler
- Her personelin yanında **düzenleme butonu** (kalem ikonu)
- Tüm personel bilgileri düzenlenebilir:
  - Ad Soyad, Unvan, Sicil No
  - Günlük Çalışma Saati
  - **Nöbet Puanı** ⭐
  - Kadro Tipi (4A, 4B, 4D, Akademik)
  - Akademik Unvan
  - SH Dışı durumu
  - Hafta Sonu ayarları

### API Güncellemeleri
- `GET /api/employees/{id}` - Tek personel bilgisi getirme
- `PUT /api/employees/{id}` - Tüm alanları güncelleme (genişletildi)

### Yeni Fonksiyonlar
- `openEditEmployee(employeeId)` - Düzenleme modal'ını aç
- `toggleEditAcademicTitle()` - Akademik unvan toggle

### Dosyalar
- `src/Nobetci.Web/Controllers/AppController.cs`
- `src/Nobetci.Web/Views/App/Index.cshtml`

---

## 3. 🏥 Admin Panel - Birim Limitleri

### Yeni Alanlar (ApplicationUser)
| Alan | Açıklama | Varsayılan |
|------|----------|------------|
| `UnitLimit` | Kullanıcının oluşturabileceği max birim sayısı | 5 |
| `UnitEmployeeLimit` | Bir birime eklenebilecek max personel sayısı | 0 (limitsiz) |

### Admin/Users/Edit Sayfası
- Yeşil gradient kutu ile "Birim Limitleri" bölümü
- Her iki limit için number input
- Açıklayıcı notlar

### Limit Kontrolleri
- `CreateUnit`: Birim oluşturmadan önce `UnitLimit` kontrolü
- `AssignEmployeesToUnit`: Personel atarken `UnitEmployeeLimit` kontrolü

### Dosyalar
- `src/Nobetci.Web/Data/Entities/ApplicationUser.cs`
- `src/Nobetci.Web/Models/AdminViewModels.cs`
- `src/Nobetci.Web/Controllers/AdminController.cs`
- `src/Nobetci.Web/Controllers/AppController.cs`
- `src/Nobetci.Web/Views/Admin/EditUser.cshtml`
- `src/Nobetci.Web/Program.cs` (SQL migration)

---

## 4. 📦 Modüler Sistem Altyapısı

### Yeni Entity'ler

#### Module (Ana Modül)
```csharp
public class Module
{
    public int Id { get; set; }
    public string Name { get; set; }        // "Hemşire Nöbet Sistemi"
    public string Code { get; set; }        // "nurse-shift"
    public string Icon { get; set; }        // "🏥"
    public string Color { get; set; }       // "#3B82F6"
    public bool IsSystem { get; set; }
    public bool IsPremium { get; set; }
    public ICollection<SubModule> SubModules { get; set; }
}
```

#### SubModule (Alt Modül)
```csharp
public class SubModule
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string Name { get; set; }        // "Nöbet Yönetimi"
    public string Code { get; set; }        // "shifts"
    public string Icon { get; set; }        // "📅"
    public string RouteUrl { get; set; }    // "/app"
    public string RequiredPermission { get; set; } // "CanAccessAttendance"
    public bool IsPremium { get; set; }
}
```

#### UserModuleAccess (Kullanıcı Erişimi)
```csharp
public class UserModuleAccess
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public int ModuleId { get; set; }
    public bool HasAccess { get; set; }
    public DateTime? AccessStartDate { get; set; }
    public DateTime? AccessEndDate { get; set; }
}
```

### Varsayılan Modüller (Seed)

#### Ana Modül: Hemşire Nöbet Sistemi
| Alt Modül | Kod | İkon | Premium | Erişim Kontrolü |
|-----------|-----|------|---------|-----------------|
| Nöbet Yönetimi | `shifts` | 📅 | ❌ | - |
| Personel Yönetimi | `employees` | 👥 | ❌ | - |
| Vardiya Şablonları | `templates` | ⏰ | ❌ | - |
| İzin Yönetimi | `leaves` | 🏖️ | ❌ | - |
| Resmi Tatiller | `holidays` | 🎉 | ❌ | - |
| Mesai Takip | `attendance` | 🕐 | ❌ | `CanAccessAttendance` |
| Puantaj | `timesheet` | 📊 | ❌ | `CanAccessPayroll` |
| Birim Yönetimi | `units` | 🏛️ | ✅ | `CanManageUnits` |
| Raporlar | `reports` | 📈 | ❌ | - |
| Excel Export | `export` | 📥 | ❌ | - |

### Veritabanı Tabloları (Program.cs)
- `Modules` - Ana modül tanımları
- `SubModules` - Alt modül tanımları
- `UserModuleAccesses` - Kullanıcı erişim hakları

### Dosyalar
- `src/Nobetci.Web/Data/Entities/Module.cs` (YENİ)
- `src/Nobetci.Web/Data/ApplicationDbContext.cs`
- `src/Nobetci.Web/Program.cs`

---

## 5. 🎨 Admin Panel - Modül Erişimleri UI

### Yeni Tasarım (EditUser.cshtml)
"Özellik Erişimi" bölümü → "Modül Erişimleri" olarak yeniden tasarlandı:

#### Hemşire Nöbet Sistemi Kartı
- Mavi gradient arka plan (`#eff6ff` → `#dbeafe`)
- "AKTİF" badge
- 2 sütunlu alt modül grid'i

#### Alt Modül Gösterimi
- **Temel modüller**: Sadece gösterim (✓ işareti)
- **Toggle edilebilir modüller**: Checkbox ile açılıp kapatılabilir
  - 🕐 Mesai Takip
  - 📊 Puantaj
- **Premium modül**: Sarı gradient vurgu
  - 🏛️ Birim Yönetimi

#### Gelecek Modüller Placeholder
- Noktalı çerçeve ile "Yakında: Yeni modüller eklenecek..."
- Hasta Takip, Stok Yönetimi, Eğitim Takip...

### CSS Stilleri
```css
.submodule-toggle:has(input:checked) {
    border-color: #3b82f6;
    background: #eff6ff;
}

.submodule-toggle.premium:has(input:checked) {
    border-color: #f59e0b;
    background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);
}
```

### Dosyalar
- `src/Nobetci.Web/Views/Admin/EditUser.cshtml`

---

## 🔮 Gelecek Planlar

### Eklenebilecek Modüller
- **Hasta Takip Sistemi** (`patient-tracking`)
- **Stok Yönetimi** (`inventory`)
- **Personel Özlük** (`hr`)
- **Eğitim Takip** (`training`)

### Yapılacaklar
- [ ] Sidebar'ı modül bazlı organize et
- [ ] Modül bazlı URL routing
- [ ] UserModuleAccess tablosunu aktif kullan
- [ ] Modül bazlı raporlama

---

## 📁 Değişen Dosyalar Özeti

| Dosya | Değişiklik |
|-------|------------|
| `AppViewModel.cs` | `AllEmployees` property eklendi |
| `ApplicationUser.cs` | `UnitLimit`, `UnitEmployeeLimit` eklendi |
| `AdminViewModels.cs` | Yeni alanlar eklendi |
| `Module.cs` | **YENİ** - Module, SubModule, UserModuleAccess entity'leri |
| `ApplicationDbContext.cs` | Module entity konfigürasyonları |
| `AppController.cs` | Personel düzenleme API, limit kontrolleri |
| `AdminController.cs` | EditUser güncellendi |
| `Index.cshtml` | Personel modal, düzenleme modal, JS fonksiyonları |
| `EditUser.cshtml` | Modül erişimleri UI |
| `Program.cs` | SQL migration, SeedModules |

---

## 🚀 Commit'ler

1. `feat: Personel modal birimden bağımsız hale getirildi`
2. `feat: Personel düzenleme özelliği eklendi`
3. `feat: Admin panelinde birim limitlerini yönetme`
4. `feat: Modüler sistem altyapısı oluşturuldu`
5. `feat: Admin kullanıcı düzenleme sayfasına modül erişimleri eklendi`

---

*Bu döküman 24 Ocak 2026 tarihinde yapılan güncellemeleri içermektedir.*

