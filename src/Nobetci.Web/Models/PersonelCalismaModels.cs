namespace Nobetci.Web.Models;

public class PersonelCalismaSonucu
{
    public bool Basarili { get; set; }
    public string? HataMesaji { get; set; }
    public string PersonelTcKimlik { get; set; } = string.Empty;
    public string PersonelAdSoyad { get; set; } = string.Empty;
    public string? KadroTipi { get; set; }
    public string? DetayliKadroTipi { get; set; }
    public bool IsAkademik { get; set; }
    public string? AkademikUnvan { get; set; }
    public int Yil { get; set; }
    public int Ay { get; set; }
    public int HaftaIciGunSayisi { get; set; }
    public int HaftaSonuGunSayisi { get; set; }
    public int ResmiTatilGunSayisi { get; set; }
    public int YarimGunTatilSayisi { get; set; }
    public bool RadyasyonAlanindaMi { get; set; }
    public double CalismaSaati { get; set; }
    public double GercekCalismaSaati { get; set; }
    public double FazlaMesaiSaati { get; set; }
    public List<DateOnly> IzinGunleri { get; set; } = new();
    public Dictionary<string, int> IzinTurleri { get; set; } = new();
    public List<NobetBilgisi> NobetGunleri { get; set; } = new();
    public Dictionary<string, double> FarkliBirimCalismaSaatleri { get; set; } = new();
    public Dictionary<string, double> FarkliGrupCalismaSaatleri { get; set; } = new();
}

public class NobetBilgisi
{
    public DateOnly Tarih { get; set; }
    public TimeOnly? BaslangicSaati { get; set; }
    public TimeOnly? BitisSaati { get; set; }
    public decimal CalismaSuresi { get; set; }
    public int NobetTuttuguBirimId { get; set; } = -1;
    public string? NobetTuttuguBirimAdi { get; set; }
    public int? GorevYaptigiGrupId { get; set; }
    public string? GorevYaptigiGrupAdi { get; set; }
    public int? GorevYaptigiGrupTipi { get; set; }
}

public class PersonelPuantajHesaplamaSonucu
{
    public bool Basarili { get; set; }
    public string? HataMesaji { get; set; }
    public string PersonelTcKimlik { get; set; } = string.Empty;
    public string PersonelAdSoyad { get; set; } = string.Empty;
    public string? PersonelUnvan { get; set; }
    public string? KadroTipi { get; set; }
    public string? DetayliKadroTipi { get; set; }
    public bool IsAkademik { get; set; }
    public string? AkademikUnvan { get; set; }
    public int MantiksalBirimId { get; set; }
    public string? BirimAdi { get; set; }
    public int Yil { get; set; }
    public int Ay { get; set; }
    public double ToplamCalismaSaati { get; set; }
    public double PlanlananCalismaSaati { get; set; }
    public double FazlaMesaiSaati { get; set; }
    public double NormalServisFazlaMesai { get; set; }
    public double YogunBakimFazlaMesai { get; set; }
    public double GeceCalismaSaati { get; set; }
    public double HaftasonuCalismaSaati { get; set; }
    public double ResmiTatilCalismaSaati { get; set; }
    public double NormalServisBayram { get; set; }
    public double YogunBakimBayram { get; set; }
    public double BayramCalismasiSaati { get; set; }
    public double BayramNobetFarkiSaati { get; set; }
    public bool BayramFarkiVar { get; set; }
    public bool YogunBakimVar { get; set; }
    public int NobetGunSayisi { get; set; }
    public int IzinGunSayisi { get; set; }
    public int YillikIzinGunSayisi { get; set; }
    public int HastalikIzinGunSayisi { get; set; }
    public int YemekOgunSayisi { get; set; }
    public int UlasimGunuSayisi { get; set; }
    public decimal CalistigiGunSayisi { get; set; }
    public int BiletSayisi { get; set; }
    public bool YogunBakimMi { get; set; }
    public List<PuantajGunlukDetay> GunlukDetaylar { get; set; } = new();
}
