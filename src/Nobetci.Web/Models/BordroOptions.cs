namespace Nobetci.Web.Models;

public class BordroOptions
{
    public decimal MemurMaasKatsayisi { get; set; } = 1.170211m;
    public decimal YogunBakimCarpani { get; set; } = 1.50m;
    public decimal NormalBayramCarpan { get; set; } = 1.25m;
    public decimal YogunBayramCarpan { get; set; } = 1.75m;
    public decimal BayramFarkiCarpan { get; set; } = 0.25m;
    public decimal DamgaVergisiOrani { get; set; } = 0.0075948m;

    public decimal SgkMaluliyetDevOrani { get; set; } = 0.11m;
    public decimal SgkGssDevOrani { get; set; } = 0.075m;
    public decimal SgkIsKazasiOrani { get; set; } = 0.02m;
    public decimal SgkMaluliyetKisiOrani { get; set; } = 0.09m;
    public decimal SgkGssKisiOrani { get; set; } = 0.05m;
}
