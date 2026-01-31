namespace Nobetci.Web.Models;

public class BordroHesaplamaOzet
{
    public int MantiksalBirimId { get; set; }
    public string BirimAdi { get; set; } = string.Empty;
    public int Yil { get; set; }
    public int Ay { get; set; }
    public string AyAdi { get; set; } = string.Empty;
    public int Bordro4APersonelSayisi { get; set; }
    public int Bordro4BPersonelSayisi { get; set; }
    public decimal Bordro4AToplamTutar { get; set; }
    public decimal Bordro4BToplamTutar { get; set; }
    public List<string> BasarisizPersonelListesi { get; set; } = new();
}

public class BordroDetayViewModel
{
    public Bordro4AResult? Bordro4A { get; set; }
    public Bordro4BResult? Bordro4B { get; set; }
    public List<string> Steps { get; set; } = new();
}
