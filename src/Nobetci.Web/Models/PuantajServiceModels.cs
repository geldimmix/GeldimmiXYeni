namespace Nobetci.Web.Models;

public class PuantajHesaplamaResult
{
    public bool Basarili { get; set; }
    public string? HataMesaji { get; set; }
    public string? Mesaj { get; set; }
    public int HesaplananPersonelSayisi { get; set; }
    public int ToplamPersonelSayisi { get; set; }
    public int PersonelSayisi { get; set; }
    public List<PersonelPuantajOzet> Personeller { get; set; } = new();
}

public class PersonelPuantajDetay
{
    public string TcKimlik { get; set; } = string.Empty;
    public string AdSoyad { get; set; } = string.Empty;
    public string? Unvan { get; set; }
    public string? KadroTipi { get; set; }
    public int Yil { get; set; }
    public int Ay { get; set; }
    public decimal ToplamCalismaSaati { get; set; }
    public decimal HedefCalismaSaati { get; set; }
    public decimal FazlaMesaiSaati { get; set; }
    public decimal NormalServisFazlaMesai { get; set; }
    public decimal YogunBakimFazlaMesai { get; set; }
    public decimal GeceCalismaSaati { get; set; }
    public decimal HaftaSonuSaati { get; set; }
    public decimal TatilSaati { get; set; }
    public decimal NormalServisBayram { get; set; }
    public decimal YogunBakimBayram { get; set; }
    public decimal BayramFarkiSaati { get; set; }
    public bool BayramFarkiVar { get; set; }
    public bool YogunBakimVar { get; set; }
    public int NobetGunSayisi { get; set; }
    public int IzinGunu { get; set; }
    public int YillikIzinGunu { get; set; }
    public int HastalikIzinGunu { get; set; }
    public int UlasimGunuSayisi { get; set; }
    public List<PuantajGunlukDetay> GunlukDetaylar { get; set; } = new();
}

public class PersonelPuantajOzet
{
    public string TcKimlik { get; set; } = string.Empty;
    public string AdSoyad { get; set; } = string.Empty;
    public string? Unvan { get; set; }
    public string? KadroTipi { get; set; }
    public decimal ToplamCalismaSaati { get; set; }
    public decimal FazlaMesaiSaati { get; set; }
}

public class PuantajOzetBilgileri
{
    public int PersonelSayisi { get; set; }
    public decimal ToplamCalismaSaati { get; set; }
    public decimal ToplamFazlaMesai { get; set; }
    public decimal ToplamGeceMesaisi { get; set; }
    public decimal ToplamTatilMesaisi { get; set; }
    public decimal ToplamHaftaSonuMesaisi { get; set; }
}

public class PuantajGunlukDetay
{
    public DateOnly Tarih { get; set; }
    public TimeOnly? MesaiBaslangic { get; set; }
    public TimeOnly? MesaiBitis { get; set; }
    public decimal CalismaSaati { get; set; }
    public decimal NormalServisFazlaMesaiSaati { get; set; }
    public decimal YogunBakimFazlaMesaiSaati { get; set; }
    public decimal FazlaMesaiSaati { get; set; }
    public decimal GeceCalismasiSaati { get; set; }
    public decimal BayramCalismasiSaati { get; set; }
    public int BiletSayisi { get; set; }
    public bool ResmiTatilMi { get; set; }
    public bool YogunBakimMi { get; set; }
    public bool IzinliMi { get; set; }
    public string? IzinTuru { get; set; }
    public string? Aciklama { get; set; }
    public decimal CalisilanGunSayisi { get; set; }
    public int? CalisilanBirimId { get; set; }
    public int? CalisilanGrupId { get; set; }
}
