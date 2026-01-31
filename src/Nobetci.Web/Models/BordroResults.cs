namespace Nobetci.Web.Models;

public class Bordro4AResult
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeTitle { get; set; }
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public string? CalisilanBirimler { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int NobetPuani { get; set; }
    public bool YogunBakimVar { get; set; }
    public decimal SaatUcreti { get; set; }

    public decimal NormalServisNobetSaati { get; set; }
    public decimal YogunBakimNobetSaati { get; set; }
    public decimal NormalServisBayramSaati { get; set; }
    public decimal YogunBakimBayramSaati { get; set; }
    public decimal BayramFarkiNobetSaati { get; set; }

    public decimal NormalServisNobetToplamTutar { get; set; }
    public decimal YogunBakimNobetToplamTutar { get; set; }
    public decimal NormalServisBayramToplamTutar { get; set; }
    public decimal YogunBakimBayramToplamTutar { get; set; }
    public decimal BayramFarkiToplamTutar { get; set; }

    public decimal GenelToplamTutar { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal EleGecenToplam { get; set; }
}

public class Bordro4BResult
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeTitle { get; set; }
    public int? UnitId { get; set; }
    public string? UnitName { get; set; }
    public string? CalisilanBirimler { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int NobetPuani { get; set; }
    public bool YogunBakimVar { get; set; }
    public decimal SaatUcreti { get; set; }

    public decimal NormalServisNobetSaati { get; set; }
    public decimal YogunBakimNobetSaati { get; set; }
    public decimal NormalServisBayramSaati { get; set; }
    public decimal YogunBakimBayramSaati { get; set; }
    public decimal BayramFarkiNobetSaati { get; set; }

    public decimal NormalServisNobetToplamTutar { get; set; }
    public decimal YogunBakimNobetToplamTutar { get; set; }
    public decimal NormalServisBayramToplamTutar { get; set; }
    public decimal YogunBakimBayramToplamTutar { get; set; }
    public decimal BayramFarkiToplamTutar { get; set; }

    public decimal GenelToplamTutarPek { get; set; }
    public decimal MaluliyetYaslilikEmeklilikDev { get; set; }
    public decimal GssDev { get; set; }
    public decimal KisaVadSigKolPrim { get; set; }
    public decimal GelirToplami { get; set; }
    public decimal DamgaVergisi { get; set; }
    public decimal MaluliyetYaslilikEmeklilikDevKesinti { get; set; }
    public decimal GssDevKesinti { get; set; }
    public decimal KisaVadSigKolPrimKesinti { get; set; }
    public decimal MaluliyetYaslilikEmeklilikKisi { get; set; }
    public decimal GssKisi { get; set; }
    public decimal KesintiToplami { get; set; }
    public decimal EleGecenToplam { get; set; }
}
