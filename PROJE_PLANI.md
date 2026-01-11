# NÖBETÇİ - Online Nöbet & Puantaj Sistemi

> Akıllı Nöbet Listesi Oluşturucu | Smart Shift Scheduling Software

---

## 📋 Proje Özeti

**Amaç:** Şirketlere/kurumlara yönelik online nöbet listesi oluşturma ve otomatik puantaj hesaplama sistemi.

**Hedef Kitle:**
- Hastaneler (hemşire, doktor nöbetleri)
- Güvenlik şirketleri
- Fabrikalar (vardiyalı çalışma)
- 7/24 hizmet veren tüm kurumlar

**İş Modeli:**
| Plan | Personel Limiti | Mesai Şablonları | Fiyat |
|------|-----------------|------------------|-------|
| Guest (Kayıtsız) | 10 kişi | Global şablonlar + Custom zaman/mola | Ücretsiz |
| Free (Kayıtlı) | 10 kişi | Kendi şablonlarını kaydedebilir | Ücretsiz |
| Freemium (Kayıtlı) | 25 kişi | Kendi şablonlarını kaydedebilir | Ücretsiz |
| Premium | Sınırsız | Gelişmiş şablonlar | TBD |

**Dil Desteği:** Türkçe + İngilizce

---

## 🌐 Sayfa Yapısı

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         SAYFA MİMARİSİ                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  1️⃣ ANA SAYFA (Landing Page)                                           │
│     ├─► SEO Hook Bölümleri                                              │
│     ├─► Özellik Tanıtımları                                             │
│     ├─► CTA: Hemen Başla                                                │
│     └─► SSS (FAQ Schema)                                                │
│                                                                         │
│  2️⃣ NÖBET OLUŞTURMA SEO İÇERİKLERİ                                     │
│     ├─► /nobet-olusturma                                                │
│     ├─► /vardiya-planlama                                               │
│     ├─► /hemsire-nobet-programi                                         │
│     ├─► /adil-nobet-dagitimi                                            │
│     └─► (Her biri ayrı SEO sayfası)                                     │
│                                                                         │
│  3️⃣ NÖBET OLUŞTURMA MODÜLÜ (Uygulama)                                  │
│     ├─► /app/nobet-listesi                                              │
│     ├─► Personel ekleme                                                 │
│     ├─► Aylık takvim/tablo                                              │
│     ├─► Mesai giriş/çıkış                                               │
│     ├─► İzin/Tatil yönetimi                                             │
│     └─► Akıllı otomatik dağıtım                                         │
│                                                                         │
│  4️⃣ PUANTAJ MODÜLÜ (Uygulama)                                          │
│     ├─► /app/puantaj                                                    │
│     ├─► Otomatik hesaplama                                              │
│     ├─► Kategori bazlı görünüm                                          │
│     ├─► Excel export (ClosedXML)                                        │
│     └─► Aylık özet rapor                                                │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🎣 Ana Sayfa Hook Bölümleri (SEO Odaklı)

### Türkçe Hook Sections

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    ANA SAYFA - TÜRKÇE HOOKLAR                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  🎯 HERO SECTION                                                        │
│     H1: "Online Nöbet Listesi Oluşturucu"                               │
│     Alt: Hemşire nöbet programı, vardiya planı, adil dağıtım            │
│                                                                         │
│  ══════════════════════════════════════════════════════════════════     │
│                                                                         │
│  📌 HOOK 1: Nöbet Oluşturma                                             │
│     H2: "Saniyeler İçinde Nöbet Listesi Oluşturun"                      │
│     Keywords: nöbet oluşturma, nöbet listesi oluşturma,                 │
│               online nöbet oluşturma, nöbet çizelgesi oluşturma         │
│     İçerik: Kolay arayüz, hızlı giriş, aylık planlama                   │
│                                                                         │
│  📌 HOOK 2: Vardiya Sistemi                                             │
│     H2: "Akıllı Vardiya Planlama Sistemi"                               │
│     Keywords: vardiya sistemi, vardiya planlama, vardiya programı,      │
│               vardiya çizelgesi, 3 vardiya sistemi                      │
│     İçerik: Esnek vardiya tanımları, gece/gündüz/akşam                  │
│                                                                         │
│  📌 HOOK 3: Puantaj Oluşturma                                           │
│     H2: "Otomatik Puantaj Hesaplama"                                    │
│     Keywords: puantaj oluşturma, puantaj hesaplama, puantaj tablosu,    │
│               aylık puantaj, personel puantaj                           │
│     İçerik: Tek tıkla puantaj, Excel export, detaylı rapor              │
│                                                                         │
│  📌 HOOK 4: Otomatik Puantaj                                            │
│     H2: "Nöbet Listesinden Otomatik Puantaj"                            │
│     Keywords: otomatik puantaj oluşturma, otomatik puantaj hesaplama,   │
│               puantaj otomasyonu, akıllı puantaj                        │
│     İçerik: Nöbet girildikçe anlık hesaplama                            │
│                                                                         │
│  📌 HOOK 5: Hemşire Nöbet Programı                                      │
│     H2: "Hemşireler İçin Profesyonel Nöbet Programı"                    │
│     Keywords: hemşire nöbet programı, hemşire nöbet listesi,            │
│               hemşire vardiya, hastane nöbet çizelgesi,                 │
│               sağlık personeli nöbet                                    │
│     İçerik: Hastane odaklı özellikler, 24 saat nöbet desteği            │
│                                                                         │
│  📌 HOOK 6: Adil Nöbet Dağıtımı                                         │
│     H2: "Adil Nöbet Dağıtımı Algoritması"                               │
│     Keywords: adil nöbet dağıtımı, adil nöbet oluşturma,                │
│               eşit nöbet dağılımı, dengeli nöbet planı,                 │
│               nöbet adaleti                                             │
│     İçerik: Akıllı algoritma, gece/hafta sonu dengesi                   │
│                                                                         │
│  📌 HOOK 7: Gece Çalışması Takibi                                       │
│     H2: "Gece Mesaisi ve Fazla Mesai Takibi"                            │
│     Keywords: gece çalışması hesaplama, gece mesaisi,                   │
│               fazla mesai hesaplama, mesai takibi                       │
│     İçerik: Otomatik gece saati hesabı, yasal uyum                      │
│                                                                         │
│  📌 HOOK 8: Excel Export                                                │
│     H2: "Excel'e Aktar, Her Yerde Kullan"                               │
│     Keywords: nöbet listesi excel, puantaj excel,                       │
│               excel nöbet şablonu, excel export                         │
│     İçerik: Tek tıkla Excel, profesyonel format                         │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### English Hook Sections

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    HOMEPAGE - ENGLISH HOOKS                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  🎯 HERO SECTION                                                        │
│     H1: "Smart Nurse Shift Scheduling Software"                         │
│     Sub: Fair distribution, automatic timesheet, instant reports        │
│                                                                         │
│  ══════════════════════════════════════════════════════════════════     │
│                                                                         │
│  📌 HOOK 1: Nurse Shift Scheduling                                      │
│     H2: "Effortless Nurse Shift Scheduling"                             │
│     Keywords: nurse shift scheduling, nurse scheduling software,        │
│               nurse roster, nurse duty schedule,                        │
│               healthcare shift planning                                 │
│     Content: Easy scheduling, drag-and-drop, monthly view               │
│                                                                         │
│  📌 HOOK 2: Nurse Shift Planner                                         │
│     H2: "Professional Nurse Shift Planner"                              │
│     Keywords: nurse shift planner, shift planner for nurses,            │
│               hospital shift planner, staff shift planner,              │
│               duty roster planner                                       │
│     Content: Built for healthcare, 24-hour shifts support               │
│                                                                         │
│  📌 HOOK 3: Nurse Shift Hours                                           │
│     H2: "Track Nurse Shift Hours Automatically"                         │
│     Keywords: nurse shift hours, nursing hours calculator,              │
│               shift hours tracking, work hours calculator,              │
│               nursing shift hours                                       │
│     Content: Automatic calculation, overtime tracking                   │
│                                                                         │
│  📌 HOOK 4: Nurse Shift Report                                          │
│     H2: "Comprehensive Nurse Shift Reports"                             │
│     Keywords: nurse shift report, shift report template,                │
│               nursing shift report, duty report,                        │
│               shift summary report                                      │
│     Content: Detailed reports, Excel export, monthly summary            │
│                                                                         │
│  📌 HOOK 5: Timesheet & Payroll                                         │
│     H2: "Automatic Timesheet Generation"                                │
│     Keywords: timesheet calculator, timesheet generator,                │
│               employee timesheet, work timesheet,                       │
│               timesheet software, timesheet template                    │
│     Content: Auto-generated timesheets, export ready                    │
│                                                                         │
│  📌 HOOK 6: Payroll Hours                                               │
│     H2: "Accurate Payroll Hours Calculation"                            │
│     Keywords: payroll hours calculator, payroll timesheet,              │
│               hours for payroll, payroll hours tracking,                │
│               overtime payroll                                          │
│     Content: Night shift premium, weekend hours, overtime               │
│                                                                         │
│  📌 HOOK 7: Fair Shift Distribution                                     │
│     H2: "AI-Powered Fair Shift Distribution"                            │
│     Keywords: fair shift distribution, equal shift allocation,          │
│               shift fairness algorithm, balanced scheduling,            │
│               equitable roster                                          │
│     Content: Smart algorithm, night/weekend balance                     │
│                                                                         │
│  📌 HOOK 8: Night Shift Tracking                                        │
│     H2: "Night Shift Hours & Overtime Tracking"                         │
│     Keywords: night shift calculator, night shift hours,                │
│               overtime calculator, night differential,                  │
│               shift differential calculator                             │
│     Content: Automatic night hour detection, legal compliance           │
│                                                                         │
│  📌 HOOK 9: Excel Export                                                │
│     H2: "Export to Excel with One Click"                                │
│     Keywords: shift schedule excel, timesheet excel export,             │
│               roster excel template, schedule spreadsheet               │
│     Content: Professional Excel format, ready to print                  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🔍 Genişletilmiş SEO Anahtar Kelimeler

### Türkçe Keywords (Tam Liste)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    TÜRKÇE ANAHTAR KELİMELER                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  🎯 NÖBET OLUŞTURMA                                                     │
│     ├─► nöbet oluşturma                                                 │
│     ├─► nöbet listesi oluşturma                                         │
│     ├─► online nöbet oluşturma                                          │
│     ├─► nöbet çizelgesi oluşturma                                       │
│     ├─► nöbet programı oluşturma                                        │
│     ├─► ücretsiz nöbet listesi oluşturma                                │
│     ├─► otomatik nöbet listesi oluşturma                                │
│     └─► aylık nöbet listesi oluşturma                                   │
│                                                                         │
│  🎯 VARDİYA SİSTEMİ                                                     │
│     ├─► vardiya sistemi                                                 │
│     ├─► vardiya planlama                                                │
│     ├─► vardiya programı                                                │
│     ├─► vardiya çizelgesi                                               │
│     ├─► 3 vardiya sistemi                                               │
│     ├─► vardiya takibi                                                  │
│     ├─► online vardiya planlama                                         │
│     └─► vardiya yönetimi                                                │
│                                                                         │
│  🎯 PUANTAJ                                                             │
│     ├─► puantaj oluşturma                                               │
│     ├─► puantaj hesaplama                                               │
│     ├─► puantaj tablosu                                                 │
│     ├─► aylık puantaj                                                   │
│     ├─► personel puantaj                                                │
│     ├─► puantaj cetveli                                                 │
│     ├─► otomatik puantaj                                                │
│     ├─► puantaj programı                                                │
│     └─► puantaj excel                                                   │
│                                                                         │
│  🎯 OTOMATİK PUANTAJ                                                    │
│     ├─► otomatik puantaj oluşturma                                      │
│     ├─► otomatik puantaj hesaplama                                      │
│     ├─► puantaj otomasyonu                                              │
│     ├─► akıllı puantaj sistemi                                          │
│     └─► nöbetten puantaj oluşturma                                      │
│                                                                         │
│  🎯 HEMŞİRE NÖBET                                                       │
│     ├─► hemşire nöbet programı                                          │
│     ├─► hemşire nöbet listesi                                           │
│     ├─► hemşire vardiya                                                 │
│     ├─► hemşire nöbet çizelgesi                                         │
│     ├─► hastane nöbet listesi                                           │
│     ├─► sağlık personeli nöbet                                          │
│     ├─► hemşire mesai takibi                                            │
│     └─► hemşire puantaj                                                 │
│                                                                         │
│  🎯 ADİL DAĞITIM                                                        │
│     ├─► adil nöbet dağıtımı                                             │
│     ├─► adil nöbet oluşturma                                            │
│     ├─► eşit nöbet dağılımı                                             │
│     ├─► dengeli nöbet planı                                             │
│     ├─► nöbet adaleti                                                   │
│     ├─► adil vardiya dağıtımı                                           │
│     └─► akıllı nöbet algoritması                                        │
│                                                                         │
│  🎯 MESAİ & GECE                                                        │
│     ├─► gece çalışması hesaplama                                        │
│     ├─► gece mesaisi                                                    │
│     ├─► fazla mesai hesaplama                                           │
│     ├─► mesai takibi                                                    │
│     ├─► hafta sonu mesaisi                                              │
│     ├─► resmi tatil mesaisi                                             │
│     └─► mesai ücreti hesaplama                                          │
│                                                                         │
│  🎯 EXCEL & RAPOR                                                       │
│     ├─► nöbet listesi excel                                             │
│     ├─► puantaj excel                                                   │
│     ├─► excel nöbet şablonu                                             │
│     ├─► vardiya excel                                                   │
│     ├─► mesai raporu                                                    │
│     └─► aylık çalışma raporu                                            │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### English Keywords (Full List)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      ENGLISH KEYWORDS                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  🎯 NURSE SHIFT SCHEDULING                                              │
│     ├─► nurse shift scheduling                                          │
│     ├─► nurse scheduling software                                       │
│     ├─► nurse roster software                                           │
│     ├─► nurse duty schedule                                             │
│     ├─► healthcare shift scheduling                                     │
│     ├─► hospital scheduling software                                    │
│     ├─► nursing schedule maker                                          │
│     ├─► free nurse scheduling software                                  │
│     └─► online nurse scheduling                                         │
│                                                                         │
│  🎯 NURSE SHIFT PLANNER                                                 │
│     ├─► nurse shift planner                                             │
│     ├─► shift planner for nurses                                        │
│     ├─► hospital shift planner                                          │
│     ├─► staff shift planner                                             │
│     ├─► duty roster planner                                             │
│     ├─► nursing shift planner                                           │
│     └─► healthcare staff planner                                        │
│                                                                         │
│  🎯 NURSE SHIFT HOURS                                                   │
│     ├─► nurse shift hours                                               │
│     ├─► nursing hours calculator                                        │
│     ├─► shift hours tracking                                            │
│     ├─► work hours calculator                                           │
│     ├─► nursing shift hours                                             │
│     ├─► nurse working hours                                             │
│     ├─► shift hour tracker                                              │
│     └─► nursing hours tracking                                          │
│                                                                         │
│  🎯 NURSE SHIFT REPORT                                                  │
│     ├─► nurse shift report                                              │
│     ├─► shift report template                                           │
│     ├─► nursing shift report                                            │
│     ├─► duty report                                                     │
│     ├─► shift summary report                                            │
│     ├─► nursing report template                                         │
│     ├─► shift handover report                                           │
│     └─► monthly shift report                                            │
│                                                                         │
│  🎯 TIMESHEET                                                           │
│     ├─► timesheet calculator                                            │
│     ├─► timesheet generator                                             │
│     ├─► employee timesheet                                              │
│     ├─► work timesheet                                                  │
│     ├─► timesheet software                                              │
│     ├─► timesheet template                                              │
│     ├─► online timesheet                                                │
│     ├─► free timesheet calculator                                       │
│     ├─► automatic timesheet                                             │
│     └─► timesheet app                                                   │
│                                                                         │
│  🎯 PAYROLL                                                             │
│     ├─► payroll hours calculator                                        │
│     ├─► payroll timesheet                                               │
│     ├─► hours for payroll                                               │
│     ├─► payroll hours tracking                                          │
│     ├─► overtime payroll                                                │
│     ├─► payroll calculator                                              │
│     ├─► shift payroll                                                   │
│     └─► nurse payroll hours                                             │
│                                                                         │
│  🎯 FAIR DISTRIBUTION                                                   │
│     ├─► fair shift distribution                                         │
│     ├─► equal shift allocation                                          │
│     ├─► shift fairness algorithm                                        │
│     ├─► balanced scheduling                                             │
│     ├─► equitable roster                                                │
│     ├─► fair rotation schedule                                          │
│     └─► equal workload distribution                                     │
│                                                                         │
│  🎯 NIGHT SHIFT & OVERTIME                                              │
│     ├─► night shift calculator                                          │
│     ├─► night shift hours                                               │
│     ├─► overtime calculator                                             │
│     ├─► night differential calculator                                   │
│     ├─► shift differential                                              │
│     ├─► night shift premium                                             │
│     ├─► weekend shift pay                                               │
│     └─► overtime hours calculator                                       │
│                                                                         │
│  🎯 EXCEL & EXPORT                                                      │
│     ├─► shift schedule excel                                            │
│     ├─► timesheet excel export                                          │
│     ├─► roster excel template                                           │
│     ├─► schedule spreadsheet                                            │
│     ├─► duty roster excel                                               │
│     ├─► shift template excel                                            │
│     └─► export schedule to excel                                        │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📄 SEO İçerik Sayfaları (Ayrı URL'ler)

### Türkçe SEO Sayfaları

| URL | H1 | Hedef Keyword |
|-----|-----|---------------|
| `/nobet-olusturma` | Online Nöbet Listesi Oluşturma | nöbet oluşturma |
| `/vardiya-planlama` | Akıllı Vardiya Planlama Sistemi | vardiya planlama |
| `/hemsire-nobet-programi` | Hemşireler İçin Nöbet Programı | hemşire nöbet programı |
| `/adil-nobet-dagitimi` | Adil Nöbet Dağıtımı Algoritması | adil nöbet dağıtımı |
| `/puantaj-hesaplama` | Online Puantaj Hesaplama | puantaj hesaplama |
| `/otomatik-puantaj` | Otomatik Puantaj Oluşturma | otomatik puantaj |
| `/gece-mesaisi-hesaplama` | Gece Mesaisi Hesaplama | gece mesaisi hesaplama |
| `/fazla-mesai-hesaplama` | Fazla Mesai Hesaplama | fazla mesai hesaplama |

### English SEO Pages

| URL | H1 | Target Keyword |
|-----|-----|----------------|
| `/en/nurse-shift-scheduling` | Nurse Shift Scheduling Software | nurse shift scheduling |
| `/en/nurse-shift-planner` | Professional Nurse Shift Planner | nurse shift planner |
| `/en/nurse-shift-hours` | Nurse Shift Hours Calculator | nurse shift hours |
| `/en/nurse-shift-report` | Nurse Shift Report Generator | nurse shift report |
| `/en/timesheet-calculator` | Free Timesheet Calculator | timesheet calculator |
| `/en/payroll-hours` | Payroll Hours Calculator | payroll hours |
| `/en/fair-shift-distribution` | Fair Shift Distribution | fair shift distribution |
| `/en/night-shift-calculator` | Night Shift Hours Calculator | night shift calculator |

---

## 🎯 MVP Özellikleri

### 1. Hesap & Kimlik Doğrulama
- **Kayıtsız Kullanım (Guest):** Hemen kullanmaya başla, 10 kişi limit
- Kayıt ol / Giriş yap
- Plan seçimi (Free/Freemium)
- Dil seçimi (TR/EN)

### 2. Mesai Şablonları

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      MESAİ ŞABLONLARI SİSTEMİ                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  👤 KAYITSIZ KULLANICI (Guest)                                          │
│     ├─► Global şablonlar (bizim tanımladığımız):                        │
│     │   • Sabah Vardiyası (08:00 - 16:00)                               │
│     │   • Akşam Vardiyası (16:00 - 00:00)                               │
│     │   • Gece Vardiyası (00:00 - 08:00)                                │
│     │   • Uzun Nöbet (08:00 - 08:00)                                    │
│     │   • Hemşire Nöbeti (16:00 - 08:00)                                │
│     │   • Tam Gün (08:00 - 17:00)                                       │
│     │   • Yarım Gün Sabah (08:00 - 12:00)                               │
│     │   • Yarım Gün Öğleden Sonra (13:00 - 17:00)                       │
│     │                                                                   │
│     ├─► Custom zaman atama: ✅ (istediği saati girebilir)               │
│     ├─► Custom mola atama: ✅ (mola süresini değiştirebilir)            │
│     └─► Şablon kaydetme: ❌ (kayıt gerekli)                             │
│                                                                         │
│  👤 KAYITLI KULLANICI (Free/Freemium)                                   │
│     ├─► Global şablonlar: ✅                                            │
│     ├─► Custom zaman atama: ✅                                          │
│     ├─► Custom mola atama: ✅                                           │
│     └─► Kendi şablonlarını kaydetme: ✅                                 │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 3. Kurum Ayarları
- Günlük çalışma hedefi (saat)
- Haftalık çalışma hedefi (saat)
- Aylık çalışma hedefi (saat)
- Mola süresi (dakika) - **çalışmaya DAHİL**
- Gece başlangıç saati (örn: 20:00)
- Gece bitiş saati (örn: 06:00)
- Hafta sonu günleri (varsayılan: Cmt-Paz)
- Fazla mesai hesaplama modu: **Günlük** veya **Aylık Denkleştirme**

### 4. Personel Yönetimi
- Personel ekleme/düzenleme/silme
- Bilgiler: Ad Soyad, Kimlik/Sicil No (string)
- Kişiye özel çalışma hedefleri (opsiyonel, genel ayarları override eder)

### 5. Nöbet/Mesai Planlama
- Ay seçimi
- Tablo görünümü (personel × gün matrisi)
- Mesai giriş/çıkış saati girişi
- **Ertesi güne sarkan mesai desteği** (16:00 - 08:00 gibi)
- İzin girişi
- Resmi tatil tanımlama
- İdari tatil tanımlama (yarım gün dahil)

### 6. Akıllı Algoritma
- Otomatik adil nöbet dağıtımı
- Gece nöbeti dengesi
- Hafta sonu nöbeti dengesi
- Resmi tatil nöbeti dengesi

### 7. Puantaj
- Aylık puantaj görüntüleme
- Günlük detay
- Aylık özet
- **Excel export (ClosedXML)**

---

## 📊 Çalışma Kategorileri

Puantajda aşağıdaki kategoriler **ayrı ayrı** gösterilecek:

| Kategori | Açıklama |
|----------|----------|
| **Normal Çalışma** | Hafta içi, gündüz saatleri |
| **Gece Çalışması** | Ayarlanabilir saat aralığı (örn: 20:00-06:00) |
| **Hafta Sonu Çalışması** | Cumartesi-Pazar (ayarlanabilir) |
| **Resmi Tatil Çalışması** | 1 Ocak, 23 Nisan, bayramlar vs. |
| **İdari Tatil Çalışması** | Cumhurbaşkanlığı kararı, arife günleri |
| **Fazla Mesai** | Hedefin üzerinde çalışılan süreler |

### Çakışma Durumu
Bir çalışma birden fazla kategoriye girebilir (örn: 1 Ocak + Pazar + Gece).
**Tüm kategoriler ayrı ayrı gösterilir**, kurum hangisini değerlendireceğine kendisi karar verir.

---

## 🧮 Hesaplama Mantığı

### Ertesi Güne Sarkan Mesai
```
Örnek: 16:00 - 08:00 (ertesi gün) = 16 saat toplam

Gece çalışması (20:00-06:00): 10 saat
Gündüz çalışması: 6 saat (16:00-20:00 + 06:00-08:00)

2 günlük hedef: 16 saat
Fazla mesai: 0 (16-16=0)
```

### Fazla Mesai Hesaplama Modları

**Günlük Mod:**
- Her gün ayrı değerlendirilir
- O günkü hedef aşımı = fazla mesai

**Aylık Denkleştirme Modu:**
- Ay sonunda toplam bakılır
- Bir gün fazla çalışıp başka gün az çalışma dengelenebilir

### Mola
- Çalışma süresine **DAHİL**
- 8 saat hedef + 1 saat mola = 08:00-17:00 (9 saat işyerinde, 8 saat puantaj)

---

## 🤖 Akıllı Nöbet Dağıtım Algoritması

### Girdiler
- Personel listesi
- Her personelin aylık çalışma hedefi
- İzinli günler
- Günlük gerekli nöbetçi sayısı
- Resmi/idari tatiller

### Kısıtlar (Kesin Uyulacak)
- İzinli günde nöbet verilemez
- 24 saat nöbet sonrası minimum dinlenme
- Maksimum ardışık gece nöbeti

### Optimizasyon Hedefleri (Adil Dağılım)
- Toplam nöbet sayısı farkı → minimize
- Gece nöbeti sayısı farkı → minimize
- Hafta sonu nöbeti sayısı farkı → minimize
- Resmi tatil nöbeti sayısı farkı → minimize

### Çıktı
- Aylık nöbet çizelgesi
- Adillik skoru gösterimi

---

## 🛠️ Teknoloji Stack

### Backend
- ASP.NET Core 8 MVC
- Entity Framework Core (Code First)
- SQLite (MVP) → PostgreSQL/MySQL (Production)
- ASP.NET Identity (Authentication)
- **ClosedXML** (Excel Export)

### Frontend
- Razor Pages + Bootstrap 5
- Vanilla JS veya Alpine.js
- Responsive/Mobile-first

### Localization
- ASP.NET Core built-in resource files (.resx)
- TR ve EN dil dosyaları

### Deployment
- Docker ready
- Herhangi bir hosting (Azure, Railway, VPS)

---

## 🌐 URL Yapısı (Güncellenmiş)

### Türkçe
```
/                              → Ana sayfa (Landing + Hooks)
/nobet-olusturma               → SEO: Nöbet oluşturma
/vardiya-planlama              → SEO: Vardiya planlama
/hemsire-nobet-programi        → SEO: Hemşire nöbet
/adil-nobet-dagitimi           → SEO: Adil dağıtım
/puantaj-hesaplama             → SEO: Puantaj hesaplama
/otomatik-puantaj              → SEO: Otomatik puantaj
/gece-mesaisi-hesaplama        → SEO: Gece mesaisi
/fazla-mesai-hesaplama         → SEO: Fazla mesai

/app/nobet-listesi             → Uygulama: Nöbet modülü
/app/puantaj                   → Uygulama: Puantaj modülü
/app/ayarlar                   → Uygulama: Ayarlar
/app/personel                  → Uygulama: Personel yönetimi

/hesap/giris                   → Giriş
/hesap/kayit                   → Kayıt
/fiyatlandirma                 → Fiyatlar
/blog/                         → Blog
```

### English
```
/en/                           → Homepage (Landing + Hooks)
/en/nurse-shift-scheduling     → SEO: Nurse scheduling
/en/nurse-shift-planner        → SEO: Shift planner
/en/nurse-shift-hours          → SEO: Shift hours
/en/nurse-shift-report         → SEO: Shift report
/en/timesheet-calculator       → SEO: Timesheet
/en/payroll-hours              → SEO: Payroll
/en/fair-shift-distribution    → SEO: Fair distribution
/en/night-shift-calculator     → SEO: Night shift

/en/app/shift-scheduler        → App: Shift module
/en/app/timesheet              → App: Timesheet module
/en/app/settings               → App: Settings
/en/app/employees              → App: Employee management

/en/account/login              → Login
/en/account/register           → Register
/en/pricing                    → Pricing
/en/blog/                      → Blog
```

---

## 📄 Meta Tags (Güncellenmiş)

### Türkçe Ana Sayfa
```html
<title>Online Nöbet Listesi Oluşturucu | Hemşire Nöbet Programı & Puantaj</title>
<meta name="description" content="Ücretsiz online nöbet listesi oluşturma, 
vardiya planlama ve otomatik puantaj hesaplama. Adil nöbet dağıtımı algoritması 
ile hemşire nöbet programı saniyeler içinde hazır. Excel'e aktar!">
<meta name="keywords" content="nöbet oluşturma, nöbet listesi, vardiya sistemi, 
puantaj oluşturma, hemşire nöbet programı, adil nöbet dağıtımı, otomatik puantaj">
```

### English Homepage
```html
<title>Nurse Shift Scheduling Software | Free Shift Planner & Timesheet</title>
<meta name="description" content="Free nurse shift scheduling, shift planning 
and automatic timesheet generation. Fair shift distribution algorithm creates 
nurse duty roster in seconds. Export to Excel!">
<meta name="keywords" content="nurse shift scheduling, nurse shift planner, 
nurse shift hours, nurse shift report, timesheet calculator, payroll hours, 
fair shift distribution">
```

---

## 📈 Teknik SEO Checklist

### Performans
- [ ] Core Web Vitals optimizasyonu
- [ ] Lazy loading images
- [ ] CSS/JS minification
- [ ] Gzip compression
- [ ] CDN kullanımı

### Mobil
- [ ] Mobile-first design
- [ ] Responsive
- [ ] Touch-friendly UI

### Yapısal
- [ ] Semantic HTML
- [ ] Proper heading hierarchy (H1 > H2 > H3)
- [ ] Internal linking (Hook'lardan SEO sayfalarına)
- [ ] Breadcrumbs
- [ ] XML Sitemap

### Schema Markup
- [ ] Organization
- [ ] SoftwareApplication
- [ ] FAQPage
- [ ] HowTo (blog için)

### International
- [ ] hreflang tags (tr, en)
- [ ] Language switcher
- [ ] Localized content

### Analytics
- [ ] Google Analytics 4
- [ ] Google Search Console
- [ ] Event tracking

---

## ✍️ Blog İçerik Planı

### Türkçe Başlangıç Yazıları
1. "Hemşire Nöbet Listesi Nasıl Yapılır? Adım Adım Rehber"
2. "Excel ile Nöbet Listesi Yapmanın 5 Dezavantajı"
3. "Adil Nöbet Dağılımı İçin 7 Altın Kural"
4. "Gece Nöbeti Hesaplama: Yasal Haklar ve Pratik Bilgiler"
5. "Puantaj Nedir? Nasıl Hesaplanır?"
6. "Otomatik Puantaj Sisteminin Avantajları"
7. "Vardiya Planlama Rehberi: En İyi Uygulamalar"

### English Launch Articles
1. "How to Create a Fair Nurse Schedule: Complete Guide"
2. "5 Reasons to Ditch Excel for Shift Scheduling"
3. "Night Shift Management: Best Practices"
4. "Automated vs Manual Scheduling: Pros and Cons"
5. "Timesheet Automation: Save Hours Every Week"
6. "Fair Shift Distribution: The Complete Algorithm Guide"
7. "Nurse Shift Hours: Tracking and Calculating Made Easy"

---

## 📁 Veri Modeli (Taslak)

### Organization (Kurum)
```
- Id
- Name
- Plan (Guest/Free/Freemium/Premium)
- Language (tr/en)
- Settings (JSON):
  - DailyWorkHours
  - WeeklyWorkHours
  - MonthlyWorkHours
  - BreakMinutes
  - NightStartTime
  - NightEndTime
  - WeekendDays
  - OvertimeCalcMode
```

### Employee (Personel)
```
- Id
- OrganizationId
- FullName
- IdentityNo
- DailyWorkHours (nullable - override)
- WeeklyWorkHours (nullable - override)
- MonthlyWorkHours (nullable - override)
- IsActive
```

### ShiftTemplate (Mesai Şablonu)
```
- Id
- OrganizationId (null = global şablon)
- Name
- StartTime
- EndTime
- SpansNextDay (bool)
- BreakMinutes
- IsGlobal (bool)
```

### Shift (Mesai/Nöbet)
```
- Id
- EmployeeId
- Date
- StartTime
- EndTime
- SpansNextDay (bool)
- BreakMinutes
- ShiftTemplateId (nullable)
- Notes
```

### SpecialDay (Özel Gün)
```
- Id
- OrganizationId
- Date
- Type (official_holiday / admin_holiday)
- Name
- IsHalfDay (bool)
- HalfDayStartTime (nullable)
```

### Leave (İzin)
```
- Id
- EmployeeId
- StartDate
- EndDate
- Type (annual / sick / unpaid / other)
- Notes
```

---

## 🚀 Geliştirme Aşamaları

### Faz 0: Altyapı (1 hafta)
- [ ] Solution yapısı
- [ ] EF Core + SQLite setup
- [ ] ASP.NET Identity
- [ ] Localization altyapısı
- [ ] Base UI template
- [ ] ClosedXML entegrasyonu

### Faz 1: Landing & SEO (1 hafta)
- [ ] Ana sayfa (Hook sections)
- [ ] SEO içerik sayfaları
- [ ] Meta tags & Schema markup
- [ ] Responsive tasarım

### Faz 2: Temel Özellikler (2 hafta)
- [ ] Kayıtsız kullanım (Guest mode)
- [ ] Kayıt/Giriş
- [ ] Kurum ayarları
- [ ] Personel CRUD
- [ ] Plan limitleri
- [ ] Global mesai şablonları
- [ ] Custom şablon kaydetme (kayıtlı kullanıcılar)

### Faz 3: Nöbet Planlama (2 hafta)
- [ ] Aylık takvim/tablo görünümü
- [ ] Mesai giriş/çıkış
- [ ] Ertesi güne sarkan mesai
- [ ] İzin girişi
- [ ] Tatil tanımlama

### Faz 4: Puantaj (1 hafta)
- [ ] Hesaplama motoru
- [ ] Kategori ayrımı
- [ ] Aylık özet
- [ ] Excel export (ClosedXML)

### Faz 5: Akıllı Algoritma (1 hafta)
- [ ] Adil dağıtım algoritması
- [ ] Otomatik nöbet oluşturma

### Faz 6: Blog & Finalizasyon (1 hafta)
- [ ] Blog altyapısı
- [ ] İlk blog yazıları
- [ ] Final SEO optimizasyonları
- [ ] Test & bug fix

---

## ❌ MVP'de Olmayacaklar (İleride Premium)

- Gelişmiş vardiya şablonları
- Mobil uygulama
- API erişimi
- Gelişmiş raporlar
- Birden fazla departman
- Cihaz entegrasyonu

---

## 📝 Notlar

- Tüm veriler kullanıcıya gösterilecek, kurum kendi yorumunu yapacak
- Çakışan kategoriler (gece + tatil gibi) ayrı ayrı listelenir
- Mola çalışma süresine dahil
- Free/Freemium farkı sadece personel limiti
- Guest kullanıcılar global şablonları kullanır, custom zaman/mola girebilir
- Kayıtlı kullanıcılar kendi şablonlarını kaydedebilir
- **ClosedXML** ile Excel export
- SEO hook'ları ana sayfada, ayrı SEO sayfaları da mevcut

---

## 🔍 Rakip Analizi & Pazar Araştırması

### Rakipler

| Uygulama | Platform | Öne Çıkan Özellik | Eksikleri |
|----------|----------|-------------------|-----------|
| **NurseShift Planner** | iOS/Android | Akıllı şablonlar, renk kodlu takvim | Puantaj yok, sadece takvim |
| **NurseGrid** | Web + Mobil | Ekip paylaşımı, takvim senkronizasyonu | Adil dağıtım algoritması yok |
| **ShiftMate** | Mobil | Yıllık istatistikler, kişisel etkinlik | Türkçe yok, sınırlı |
| **Snap Schedule 365** | Web | Kurumsal, self-service portal | Pahalı, karmaşık |

### Kullanıcıların En Çok Dikkat Ettiği Özellikler

```
┌─────────────────────────────────────────────────────────────────────────┐
│              KULLANICILARIN BEKLENTİLERİ (Araştırma Sonucu)             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  1️⃣ HIZ VE KOLAYLIK (EN ÖNEMLİ!)                                       │
│     ├─► Saniyeler içinde vardiya ekleme                                 │
│     ├─► Akıllı şablonlar (tek tıkla atama)                              │
│     ├─► Hızlı seçim menüleri                                            │
│     └─► Sürükle-bırak arayüz                                            │
│                                                                         │
│  2️⃣ GÖRSEL NETLİK                                                      │
│     ├─► Renk kodlu vardiyalar (Gündüz: Mavi, Akşam: Turuncu, Gece: Mor) │
│     ├─► Net takvim görünümü                                             │
│     ├─► Bir bakışta tüm ay                                              │
│     └─► Light/Dark mode                                                 │
│                                                                         │
│  3️⃣ TEKRARLAYAN VARDİYA & OTOMASYON                                    │
│     ├─► Günlük/Haftalık/Aylık tekrar                                    │
│     ├─► Rotasyon şablonları (2 haftalık döngü gibi)                     │
│     ├─► Aylarca süren programları otomatik oluşturma                    │
│     └─► Bir kez ayarla, otomatik tekrarla                               │
│                                                                         │
│  4️⃣ ÇALIŞMA SAATLERİ & FAZLA MESAİ TAKİBİ                              │
│     ├─► Haftalık toplam saat                                            │
│     ├─► Aylık toplam saat                                               │
│     ├─► Otomatik fazla mesai hesaplama                                  │
│     ├─► İş yükü görselleştirme (grafik)                                 │
│     └─► Hedef vs gerçekleşen karşılaştırma                              │
│                                                                         │
│  5️⃣ PAYLAŞIM & KOORDİNASYON                                            │
│     ├─► Takvimi metin/e-posta ile paylaşma                              │
│     ├─► Görsel olarak export (resim)                                    │
│     ├─► Aile/arkadaşlarla paylaşım                                      │
│     └─► Ekip üyeleriyle senkronizasyon                                  │
│                                                                         │
│  6️⃣ ÇOKLU LOKASYON DESTEĞİ                                             │
│     ├─► Birden fazla hastane/klinik                                     │
│     ├─► Her lokasyon için ayrı renk                                     │
│     └─► Filtreleme seçenekleri                                          │
│                                                                         │
│  7️⃣ KİŞİSELLEŞTİRME                                                    │
│     ├─► Özel vardiya renkleri                                           │
│     ├─► Özel simgeler/ikonlar                                           │
│     ├─► Arka plan temaları                                              │
│     └─► Dark/Light mode                                                 │
│                                                                         │
│  8️⃣ İSTATİSTİKLER & RAPORLAR                                           │
│     ├─► Yıllık vardiya sayıları                                         │
│     ├─► Çalışma desenleri analizi                                       │
│     ├─► İş-yaşam dengesi metrikleri                                     │
│     └─► Trend görselleştirme                                            │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Bizim Rekabet Avantajlarımız

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    FARK YARATACAK ÖZELLİKLER                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  🏆 RAKİPLERDE OLMAYAN (Unique Selling Points)                          │
│                                                                         │
│  1. 🤖 ADİL DAĞITIM ALGORİTMASI                                         │
│     └─► Rakiplerde YOK! Manuel planlama yapıyorlar                      │
│     └─► Gece/hafta sonu/tatil dengeli dağıtım                           │
│     └─► Adillik skoru gösterimi                                         │
│                                                                         │
│  2. 📋 OTOMATİK PUANTAJ OLUŞTURMA                                       │
│     └─► Rakiplerde YOK! Sadece takvim sunuyorlar                        │
│     └─► Nöbet girildikçe anlık hesaplama                                │
│     └─► Gece/hafta sonu/tatil kategorileri                              │
│                                                                         │
│  3. 🇹🇷 TÜRKİYE'YE ÖZEL                                                 │
│     └─► Tam Türkçe dil desteği                                          │
│     └─► Türk resmi tatilleri entegre                                    │
│     └─► Türk çalışma mevzuatına uygun                                   │
│                                                                         │
│  4. 💰 ÜCRETSİZ KULLANIM                                                │
│     └─► Rakipler genelde ücretli veya çok sınırlı                       │
│     └─► 10 kişiye kadar tamamen ücretsiz                                │
│     └─► Kayıtsız bile kullanılabilir                                    │
│                                                                         │
│  5. 📊 DETAYLI KATEGORİ AYRIMI                                          │
│     └─► Gece çalışması ayrı                                             │
│     └─► Hafta sonu ayrı                                                 │
│     └─► Resmi tatil ayrı                                                │
│     └─► İdari tatil ayrı                                                │
│     └─► Çakışan kategoriler de gösteriliyor                             │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Rakiplerin Eksikleri (Bizim Fırsatımız)

| Eksiklik | Bizim Çözümümüz |
|----------|-----------------|
| Otomatik adil dağıtım yok | ✅ Akıllı algoritma ile saniyede adil dağıtım |
| Puantaj hesaplama yok | ✅ Otomatik puantaj, kategorili raporlama |
| Gece/hafta sonu/tatil ayrımı detaylı değil | ✅ Her kategori ayrı ayrı gösteriliyor |
| Türkçe dil desteği zayıf | ✅ Tam Türkçe, Türkiye'ye özel |
| Excel export sınırlı veya premium | ✅ Ücretsiz Excel export |
| Kayıt olmadan kullanamıyorsun | ✅ Guest mode ile hemen başla |

---

## ✅ MVP Öncelik Listesi (Araştırma Bazlı)

### 🔴 Mutlaka Olması Gerekenler (Must-Have)

| # | Özellik | Neden Önemli |
|---|---------|--------------|
| 1 | ⚡ Hızlı vardiya ekleme (şablonlarla) | Kullanıcıların #1 beklentisi |
| 2 | 🎨 Renk kodlu görünüm | Görsel netlik için kritik |
| 3 | 📊 Otomatik saat/fazla mesai hesaplama | Puantaj için gerekli |
| 4 | 📤 Excel export | Kurumsal kullanım için şart |
| 5 | 🔄 Tekrarlayan vardiya desteği | Zaman tasarrufu |
| 6 | 🌙 Gece çalışması ayrı gösterimi | Fark yaratan özellik |
| 7 | 🤖 Adil dağıtım algoritması | USP - rakiplerde yok |
| 8 | 📋 Otomatik puantaj | USP - rakiplerde yok |

### 🟡 Olması İyi Olur (Nice-to-Have)

| # | Özellik | Not |
|---|---------|-----|
| 1 | 🌓 Dark mode | Kullanıcı deneyimi |
| 2 | 📊 Adillik skoru gösterimi | Algoritma görselleştirme |
| 3 | 📱 Responsive tasarım | Mobil kullanım |
| 4 | 🔔 Bildirimler | Sonraki fazda |
| 5 | 🔗 Paylaşım linki | Sonraki fazda |

### 🟢 İleride Eklenecek (Future)

| # | Özellik | Not |
|---|---------|-----|
| 1 | 📱 Mobil uygulama | Premium |
| 2 | 🔌 API erişimi | Premium |
| 3 | 👥 Çoklu departman | Premium |
| 4 | 📈 Gelişmiş raporlar | Premium |

---

## 🎨 UI/UX Gereksinimleri (Araştırma Bazlı)

### Renk Kodlama Sistemi

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      VARDİYA RENK KODLARI                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  🔵 GÜNDÜZ VARDİYASI (06:00 - 14:00)     → Mavi tonları                │
│  🟠 AKŞAM VARDİYASI (14:00 - 22:00)      → Turuncu tonları             │
│  🟣 GECE VARDİYASI (22:00 - 06:00)       → Mor tonları                 │
│  🟢 TAM GÜN (08:00 - 17:00)              → Yeşil tonları               │
│  🔴 UZUN NÖBET (24 saat)                 → Kırmızı tonları             │
│  ⚪ İZİN                                  → Gri                         │
│  🟡 RESMİ TATİL                          → Sarı arka plan              │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Tablo Görünümü Tasarımı

```
┌─────────────────────────────────────────────────────────────────────────┐
│  📅 OCAK 2026                              [◀ Önceki] [Sonraki ▶]       │
├─────────────────────────────────────────────────────────────────────────┤
│       │ Pzt │ Sal │ Çar │ Per │ Cum │ Cmt │ Paz │ Toplam │              │
│       │  1  │  2  │  3  │  4  │  5  │  6  │  7  │  Saat  │              │
├───────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼────────┤              │
│ 👤 Ali│ 🔵  │  -  │ 🟣  │ ↓↓  │  -  │ 🔵  │  -  │  48s   │              │
│       │08-16│     │16-08│     │     │08-16│     │        │              │
├───────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼────────┤              │
│👤 Ayşe│ 🟣  │ ↓↓  │  -  │ 🔵  │  -  │  -  │ 🟣  │  56s   │              │
│       │16-08│     │     │08-16│     │     │16-08│        │              │
├───────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼────────┤              │
│👤Murat│  -  │ 🔵  │ 🔵  │  -  │ ⚪  │ ⚪  │  -  │  32s   │              │
│       │     │08-16│08-16│     │İZİN │İZİN │     │        │              │
└───────┴─────┴─────┴─────┴─────┴─────┴─────┴─────┴────────┘              │
│                                                                         │
│  Semboller: 🔵 Gündüz │ 🟠 Akşam │ 🟣 Gece │ ↓↓ Devam │ ⚪ İzin        │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 📝 Notlar

- Tüm veriler kullanıcıya gösterilecek, kurum kendi yorumunu yapacak
- Çakışan kategoriler (gece + tatil gibi) ayrı ayrı listelenir
- Mola çalışma süresine dahil
- Free/Freemium farkı sadece personel limiti
- Guest kullanıcılar global şablonları kullanır, custom zaman/mola girebilir
- Kayıtlı kullanıcılar kendi şablonlarını kaydedebilir
- **ClosedXML** ile Excel export
- SEO hook'ları ana sayfada, ayrı SEO sayfaları da mevcut
- **Renk kodlu vardiya görünümü** kullanıcı deneyimi için kritik
- **Hız ve kolaylık** en önemli kullanıcı beklentisi

---

*Son Güncelleme: Ocak 2026*
