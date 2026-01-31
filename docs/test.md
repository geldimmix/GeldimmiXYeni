# Bordro Test Akisi

Bu dokuman, uygulama calisirken bordro akisini elle test etmek icin kisa bir yol haritasi verir.

## 1) Uygulamayi Ac

- Uygulama calisiyor olmali.
- Tarayicida `/app` ve `/admin` sayfalarini ac.

## 2) On Kosullar

- En az 1 aktif personel (4A veya 4B) olustur.
- Personel icin ilgili ayda vardiya veya mesai kaydi olustur.
- Bordro sabitleri mevcut olmali.

## 3) Kullanici Tarafi Testleri

1. `/app/payroll` ac.
2. Yil/ay sec ve **Puantaj Hesapla** calistir.
3. **Bordro Hesapla** calistir (sonuclar DB'ye yazilir).
4. Tablo satirlarindan **Detay** linki ile `Puantaj Detail` ekranini ac.

Beklenen:
- Normal/Yogun saat ve tutarlar gorunur.
- Bayram, hafta sonu, gece hesaplari dogru gorunur.

## 4) Admin Tarafi Testleri

1. `/admin` ile admin paneline gir.
2. `/admin/bordro` sayfasini ac.
3. **Personel Puan Yonetimi**: yeni kayit ekle veya guncelle.
4. **Toplu Bordro Hesaplama**: donem secip calistir.
5. **Tek Personel Bordro Hesapla**: TC + yil + ay gir, sonucu kontrol et.

Beklenen:
- Tek personel JSON ozet cikti verir.
- Toplu hesaplama toplam tutarlari gosterir.

## 5) Endpoint Testleri (istege bagli)

- Tek personel (GET):
  - `/Bordro/TekPersonelBordroHesapla?tcKimlik=...&yil=2025&ay=11&yenidenHesapla=false`
- Harici API (POST):
  - `/Bordro/Api/HesaplaBordro`
  - Body: `tcKimlik`, `yil`, `ay`, `yenidenHesapla`

## 6) Dogrulama Ipuclari

- Kullanici ekrani, admin ozetleri ve export sonuclari birbiriyle tutarli olmali.
- 4A/4B ayrimi dogru olmali.
