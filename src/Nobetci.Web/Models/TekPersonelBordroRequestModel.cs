namespace Nobetci.Web.Models;

public class TekPersonelBordroRequestModel
{
    public string TcKimlik { get; set; } = string.Empty;
    public int Yil { get; set; }
    public int Ay { get; set; }
    public bool YenidenHesapla { get; set; }
}
