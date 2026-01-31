namespace Nobetci.Web.Helpers;

public static class FazlaMesaiLimitHelper
{
    public static (decimal normalServis, decimal yogunBakim) LimitFazlaMesaiSaati(decimal normalServis, decimal yogunBakim, decimal maxSaat = 130)
    {
        var toplam = normalServis + yogunBakim;
        if (toplam <= maxSaat)
            return (normalServis, yogunBakim);

        if (yogunBakim >= maxSaat)
            return (0, maxSaat);

        var kullanilabilirNormal = maxSaat - yogunBakim;
        return (kullanilabilirNormal >= 0 ? kullanilabilirNormal : 0, yogunBakim);
    }
}
