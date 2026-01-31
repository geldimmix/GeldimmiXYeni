namespace Nobetci.Web.Helpers;

public static class DecimalHelper
{
    public static decimal YuvarlaIkiHane(decimal deger)
    {
        return Math.Round(deger, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal YuzdeHesapla(decimal anaRakam, decimal oran)
    {
        return YuvarlaIkiHane(anaRakam * oran);
    }

    public static decimal CarpVeYuvarla(decimal deger1, decimal deger2)
    {
        return YuvarlaIkiHane(deger1 * deger2);
    }
}
