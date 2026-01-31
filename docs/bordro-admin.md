# Bordro Admin Dokumani

Bu dokuman, proje sahiplerinin kullandigi **ic admin panel** ve uygulama tarafindaki bordro ekranlarinin nasil calistigini aciklar.

## Erisim ve Yetki

- Admin panel girisi: `/admin` (sadece proje sahipleri)
- Musteri/son kullanici icin bordro yonetimi **/app** tarafindadir.

## Admin Panelinden Kontrol Edilecekler

Uygulama tarafinda bordro icin su sayfalar kullanilir:

- **Bordro Sabitleri**: `/bordro/sabitler`
  - Bordro katsayilari ve oranlar burada tutulur.
  - Degisimler `BordroSabitleriGecmis` tablosunda loglanir.
- **Yetkili Yonetimi**: `/bordro/yetkiliyonetimi`
  - Birim bazinda bordro yetkilisi ekleme/cikarma.
- **Personel Puan Import**: `/bordro/personelpuanimport`
  - Excel ile personel nobet puani ve saat ucretleri yuklenir.
- **Personel Puan Yonetimi**: `/bordro/personelpuanyonetimi`
  - Tekil personel puanlarini elle ekleme/guncelleme.
- **Toplu Bordro Hesaplama**: `/bordro/toplubordrohesaplama` (uygulama tarafi)
- **Tek Personel Bordro Hesapla**: `/bordro/tekpersonelbordrohesapla` (uygulama tarafi)

## Bordro Akisi (Ozet)

1. **Puantaj hesapla**: `/app/payroll` sayfasinda ilgili ay icin puantaj hesaplanir.
2. **Bordro hesapla**: ayni ekranda bordro hesaplamalari tetiklenir ve sonuc tablolara kaydedilir.
3. **Toplu ozet**: `/bordro/toplubordrohesaplama` ile 4A/4B toplam tutar ve personel sayilari gorulur.

## GetBordroDetayWithSteps

Bu endpoint, personel bordro detayini adim adim dondurur:

- URL: `/bordro/getbordrodetaywithsteps?personelTc=...&yil=2025&ay=11`
- Donen JSON:
  - `kadroTipi` (4A/4B)
  - `bordro` (kayitli bordro sonucu)
  - `steps` (hesap adimlari metin listesi)

Ornek adimlar:
- 4A: Saat ucreti, normal/yb nobet tutari, bayram farki, genel toplam, damga vergisi, net
- 4B: PEK, SGK isveren payi, gelir/kesinti toplami, net

## Harici API - Tek Personel Bordro

- POST `/Bordro/Api/HesaplaBordro`
- Body:
  - `tcKimlik`, `yil`, `ay`, `yenidenHesapla`

## Kontrol Listesi

- Bordro sabitleri guncel mi?
- Personel puanlari import edildi mi?
- Birim bordro yetkileri dogru mu?
- Puantaj ve bordro hesaplamalari ilgili ay icin tamamlandi mi?

## Notlar

- Toplu bordro sayfasi, mevcut bordro sonuclarindan ozet uretir.
- Detayli bordro goruntulemek icin once puantaj/bordro hesaplamasi yapilmalidir.
