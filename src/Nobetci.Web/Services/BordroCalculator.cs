using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;

namespace Nobetci.Web.Services;

public interface IBordroCalculator
{
    List<Bordro4AResult> Calculate4A(List<EmployeePayroll> payrolls, BordroOptions options, int year, int month, Dictionary<string, int>? puanMap = null);
    List<Bordro4BResult> Calculate4B(List<EmployeePayroll> payrolls, BordroOptions options, int year, int month, Dictionary<string, int>? puanMap = null);
}

public class BordroCalculator : IBordroCalculator
{
    private const int ArastirmaGorevlisiPuan = 145;

    public List<Bordro4AResult> Calculate4A(List<EmployeePayroll> payrolls, BordroOptions options, int year, int month, Dictionary<string, int>? puanMap = null)
    {
        var results = new List<Bordro4AResult>();
        foreach (var payroll in payrolls.Where(p => string.Equals(p.Employee.PositionType, "4A", StringComparison.OrdinalIgnoreCase)))
        {
            var unitCoefficient = ResolveUnitCoefficient(payroll.Employee.Unit);

            var nobetPuani = ResolveNobetPuani(payroll, puanMap);
            if (nobetPuani == 0)
            {
                results.Add(Build4AXResult(payroll, year, month));
                continue;
            }

            var temelSaatUcreti = Math.Round(nobetPuani * options.MemurMaasKatsayisi, 2);
            temelSaatUcreti = Math.Round(temelSaatUcreti * unitCoefficient, 2);

            var normalFazlaMesai = payroll.NormalOvertimeHours;
            var yogunBakimFazlaMesai = payroll.IntensiveOvertimeHours;
            var normalBayramSaati = payroll.NormalHolidayHours;
            var yogunBakimBayramSaati = payroll.IntensiveHolidayHours;
            var bayramFarkiSaati = payroll.HolidayDifferenceHours;

            var normalFazlaMesaiTutar = Math.Round(normalFazlaMesai * temelSaatUcreti, 2);
            var yogunBakimFazlaMesaiTutar = Math.Round(yogunBakimFazlaMesai * temelSaatUcreti * options.YogunBakimCarpani, 2);
            var normalBayramTutar = Math.Round(normalBayramSaati * temelSaatUcreti * options.NormalBayramCarpan, 2);
            var yogunBayramTutar = Math.Round(yogunBakimBayramSaati * temelSaatUcreti * options.YogunBayramCarpan, 2);
            var bayramFarkiTutar = Math.Round(bayramFarkiSaati * temelSaatUcreti * options.BayramFarkiCarpan, 2);

            var toplamBayramSaati = normalBayramSaati + yogunBakimBayramSaati;
            var normalBayramFarkiTutar = 0m;
            var yogunBayramFarkiTutar = 0m;
            var normalBayramFarkiSaati = 0m;
            var yogunBayramFarkiSaati = 0m;

            if (toplamBayramSaati > 0 && bayramFarkiSaati > 0)
            {
                var normalOran = normalBayramSaati / toplamBayramSaati;
                var yogunOran = yogunBakimBayramSaati / toplamBayramSaati;
                normalBayramFarkiTutar = Math.Round(bayramFarkiTutar * normalOran, 2);
                yogunBayramFarkiTutar = Math.Round(bayramFarkiTutar * yogunOran, 2);
                normalBayramFarkiSaati = Round2(bayramFarkiSaati * normalOran);
                yogunBayramFarkiSaati = Round2(bayramFarkiSaati * yogunOran);
            }
            else if (bayramFarkiSaati > 0 && toplamBayramSaati == 0)
            {
                normalBayramFarkiTutar = bayramFarkiTutar;
                normalBayramFarkiSaati = Round2(bayramFarkiSaati);
            }

            var hasNormal = normalFazlaMesai > 0 || normalBayramSaati > 0 || normalBayramFarkiSaati > 0;
            var hasYogun = yogunBakimFazlaMesai > 0 || yogunBakimBayramSaati > 0 || yogunBayramFarkiSaati > 0;

            if (hasNormal || (!hasNormal && !hasYogun))
            {
                var genelToplam = Math.Round(normalFazlaMesaiTutar + normalBayramTutar + normalBayramFarkiTutar, 2);
                var damga = Math.Round(genelToplam * options.DamgaVergisiOrani, 2);
                results.Add(new Bordro4AResult
                {
                    EmployeeId = payroll.Employee.Id,
                    EmployeeName = payroll.Employee.FullName,
                    EmployeeTitle = payroll.Employee.Title,
                    UnitId = payroll.Employee.UnitId,
                    UnitName = payroll.Employee.Unit?.Name,
                    CalisilanBirimler = payroll.Employee.UnitId?.ToString(),
                    Year = year,
                    Month = month,
                    NobetPuani = nobetPuani,
                    YogunBakimVar = false,
                    SaatUcreti = temelSaatUcreti,
                    NormalServisNobetSaati = Round2(normalFazlaMesai),
                    YogunBakimNobetSaati = 0,
                    NormalServisBayramSaati = Round2(normalBayramSaati),
                    YogunBakimBayramSaati = 0,
                    BayramFarkiNobetSaati = normalBayramFarkiSaati,
                    NormalServisNobetToplamTutar = normalFazlaMesaiTutar,
                    YogunBakimNobetToplamTutar = 0,
                    NormalServisBayramToplamTutar = normalBayramTutar,
                    YogunBakimBayramToplamTutar = 0,
                    BayramFarkiToplamTutar = normalBayramFarkiTutar,
                    GenelToplamTutar = genelToplam,
                    DamgaVergisi = damga,
                    EleGecenToplam = Math.Round(genelToplam - damga, 2)
                });
            }

            if (hasYogun)
            {
                var genelToplam = Math.Round(yogunBakimFazlaMesaiTutar + yogunBayramTutar + yogunBayramFarkiTutar, 2);
                var damga = Math.Round(genelToplam * options.DamgaVergisiOrani, 2);
                results.Add(new Bordro4AResult
                {
                    EmployeeId = payroll.Employee.Id,
                    EmployeeName = payroll.Employee.FullName,
                    EmployeeTitle = payroll.Employee.Title,
                    UnitId = payroll.Employee.UnitId,
                    UnitName = payroll.Employee.Unit?.Name,
                    CalisilanBirimler = payroll.Employee.UnitId?.ToString(),
                    Year = year,
                    Month = month,
                    NobetPuani = nobetPuani,
                    YogunBakimVar = true,
                    SaatUcreti = temelSaatUcreti,
                    NormalServisNobetSaati = 0,
                    YogunBakimNobetSaati = Round2(yogunBakimFazlaMesai),
                    NormalServisBayramSaati = 0,
                    YogunBakimBayramSaati = Round2(yogunBakimBayramSaati),
                    BayramFarkiNobetSaati = yogunBayramFarkiSaati,
                    NormalServisNobetToplamTutar = 0,
                    YogunBakimNobetToplamTutar = yogunBakimFazlaMesaiTutar,
                    NormalServisBayramToplamTutar = 0,
                    YogunBakimBayramToplamTutar = yogunBayramTutar,
                    BayramFarkiToplamTutar = yogunBayramFarkiTutar,
                    GenelToplamTutar = genelToplam,
                    DamgaVergisi = damga,
                    EleGecenToplam = Math.Round(genelToplam - damga, 2)
                });
            }
        }

        return results;
    }

    public List<Bordro4BResult> Calculate4B(List<EmployeePayroll> payrolls, BordroOptions options, int year, int month, Dictionary<string, int>? puanMap = null)
    {
        var results = new List<Bordro4BResult>();
        foreach (var payroll in payrolls.Where(p => string.Equals(p.Employee.PositionType, "4B", StringComparison.OrdinalIgnoreCase)))
        {
            var unitCoefficient = ResolveUnitCoefficient(payroll.Employee.Unit);

            var nobetPuani = ResolveNobetPuani(payroll, puanMap);
            if (nobetPuani == 0)
            {
                results.Add(new Bordro4BResult
                {
                    EmployeeId = payroll.Employee.Id,
                    EmployeeName = payroll.Employee.FullName,
                    EmployeeTitle = payroll.Employee.Title,
                    UnitId = payroll.Employee.UnitId,
                    UnitName = payroll.Employee.Unit?.Name,
                    CalisilanBirimler = payroll.Employee.UnitId?.ToString(),
                    Year = year,
                    Month = month,
                    NobetPuani = 0,
                    YogunBakimVar = false
                });
                continue;
            }

            var temelSaatUcreti = Math.Round(nobetPuani * options.MemurMaasKatsayisi, 2);
            temelSaatUcreti = Math.Round(temelSaatUcreti * unitCoefficient, 2);

            var normalFazlaMesai = payroll.NormalOvertimeHours;
            var yogunBakimFazlaMesai = payroll.IntensiveOvertimeHours;
            var normalBayramSaati = payroll.NormalHolidayHours;
            var yogunBakimBayramSaati = payroll.IntensiveHolidayHours;
            var bayramFarkiSaati = payroll.HolidayDifferenceHours;

            var isIntensiveCare = payroll.IsIntensiveCare;
            if (isIntensiveCare && yogunBakimFazlaMesai <= 0 && yogunBakimBayramSaati <= 0 && (normalFazlaMesai > 0 || normalBayramSaati > 0))
            {
                yogunBakimFazlaMesai = normalFazlaMesai;
                yogunBakimBayramSaati = normalBayramSaati;
                normalFazlaMesai = 0;
                normalBayramSaati = 0;
            }

            var yogunBakimVar = isIntensiveCare || yogunBakimFazlaMesai > 0 || yogunBakimBayramSaati > 0;
            var saatUcreti = yogunBakimVar ? Math.Round(temelSaatUcreti * 1.5m, 2) : temelSaatUcreti;

            var normalFazlaMesaiTutar = Math.Round(normalFazlaMesai * temelSaatUcreti, 2);
            var yogunBakimFazlaMesaiTutar = Math.Round(yogunBakimFazlaMesai * temelSaatUcreti * options.YogunBakimCarpani, 2);
            var normalBayramTutar = Math.Round(normalBayramSaati * temelSaatUcreti * options.NormalBayramCarpan, 2);
            var yogunBayramTutar = Math.Round(yogunBakimBayramSaati * temelSaatUcreti * options.YogunBayramCarpan, 2);
            var bayramFarkiTutar = Math.Round(bayramFarkiSaati * temelSaatUcreti * options.BayramFarkiCarpan, 2);

            var toplamBayramSaati = normalBayramSaati + yogunBakimBayramSaati;
            var normalBayramFarkiTutar = 0m;
            var yogunBayramFarkiTutar = 0m;
            var normalBayramFarkiSaati = 0m;
            var yogunBayramFarkiSaati = 0m;

            if (toplamBayramSaati > 0 && bayramFarkiSaati > 0)
            {
                var normalOran = normalBayramSaati / toplamBayramSaati;
                var yogunOran = yogunBakimBayramSaati / toplamBayramSaati;
                normalBayramFarkiTutar = Math.Round(bayramFarkiTutar * normalOran, 2);
                yogunBayramFarkiTutar = Math.Round(bayramFarkiTutar * yogunOran, 2);
                normalBayramFarkiSaati = Round2(bayramFarkiSaati * normalOran);
                yogunBayramFarkiSaati = Round2(bayramFarkiSaati * yogunOran);
            }
            else if (bayramFarkiSaati > 0 && toplamBayramSaati == 0)
            {
                normalBayramFarkiTutar = bayramFarkiTutar;
                normalBayramFarkiSaati = Round2(bayramFarkiSaati);
            }

            var hasNormal = normalFazlaMesai > 0 || normalBayramSaati > 0 || normalBayramFarkiSaati > 0;
            var hasYogun = yogunBakimFazlaMesai > 0 || yogunBakimBayramSaati > 0 || yogunBayramFarkiSaati > 0;

            if (hasNormal || (!hasNormal && !hasYogun))
            {
                var normalResult = Build4BResult(
                    payroll,
                    year,
                    month,
                    nobetPuani,
                    yogunBakimVar,
                    saatUcreti,
                    normalFazlaMesai,
                    0,
                    normalBayramSaati,
                    0,
                    normalBayramFarkiSaati,
                    normalFazlaMesaiTutar,
                    0,
                    normalBayramTutar,
                    0,
                    normalBayramFarkiTutar,
                    options);
                if (normalResult != null)
                    results.Add(normalResult);
            }

            if (hasYogun)
            {
                var yogunResult = Build4BResult(
                    payroll,
                    year,
                    month,
                    nobetPuani,
                    true,
                    saatUcreti,
                    0,
                    yogunBakimFazlaMesai,
                    0,
                    yogunBakimBayramSaati,
                    yogunBayramFarkiSaati,
                    0,
                    yogunBakimFazlaMesaiTutar,
                    0,
                    yogunBayramTutar,
                    yogunBayramFarkiTutar,
                    options);
                if (yogunResult != null)
                    results.Add(yogunResult);
            }
        }

        return results;
    }

    private static int ResolveNobetPuani(EmployeePayroll payroll, Dictionary<string, int>? puanMap)
    {
        var identityNo = payroll.Employee.IdentityNo ?? string.Empty;
        if (!string.IsNullOrEmpty(identityNo) && puanMap != null && puanMap.TryGetValue(identityNo, out var puan))
            return puan;

        if (payroll.Employee.ShiftScore > 0)
            return payroll.Employee.ShiftScore;

        var title = payroll.Employee.Title ?? string.Empty;
        if (title.Contains("ARAŞTIRMA", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("ASİSTAN", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("ASSISTANT", StringComparison.OrdinalIgnoreCase))
        {
            return ArastirmaGorevlisiPuan;
        }

        return 0;
    }

    private static decimal ResolveUnitCoefficient(Data.Entities.Unit? unit)
    {
        if (unit == null)
            return 1.0m;

        if (unit.Coefficient > 0 && unit.Coefficient != 1.0m)
            return unit.Coefficient;

        if (unit.UnitType?.DefaultCoefficient > 0)
            return unit.UnitType.DefaultCoefficient;

        return unit.Coefficient > 0 ? unit.Coefficient : 1.0m;
    }

    private static Bordro4AResult Build4AXResult(EmployeePayroll payroll, int year, int month)
    {
        return new Bordro4AResult
        {
            EmployeeId = payroll.Employee.Id,
            EmployeeName = payroll.Employee.FullName,
            EmployeeTitle = payroll.Employee.Title,
            UnitId = payroll.Employee.UnitId,
            UnitName = payroll.Employee.Unit?.Name,
            CalisilanBirimler = payroll.Employee.UnitId?.ToString(),
            Year = year,
            Month = month,
            NobetPuani = 0,
            YogunBakimVar = false
        };
    }

    private static Bordro4BResult? Build4BResult(
        EmployeePayroll payroll,
        int year,
        int month,
        int nobetPuani,
        bool yogunBakimVar,
        decimal saatUcreti,
        decimal normalFazlaMesai,
        decimal yogunBakimFazlaMesai,
        decimal normalBayramSaati,
        decimal yogunBakimBayramSaati,
        decimal bayramFarkiSaati,
        decimal normalFazlaMesaiTutar,
        decimal yogunBakimFazlaMesaiTutar,
        decimal normalBayramTutar,
        decimal yogunBakimBayramTutar,
        decimal bayramFarkiTutar,
        BordroOptions options)
    {
        var genelToplamPek = Math.Round(normalFazlaMesaiTutar + yogunBakimFazlaMesaiTutar + normalBayramTutar + yogunBakimBayramTutar + bayramFarkiTutar, 2);
        var maluliyetDev = Math.Round(genelToplamPek * options.SgkMaluliyetDevOrani, 2);
        var gssDev = Math.Round(genelToplamPek * options.SgkGssDevOrani, 2);
        var kisaVad = Math.Round(genelToplamPek * options.SgkIsKazasiOrani, 2);
        var gelirToplam = Math.Round(genelToplamPek + maluliyetDev + gssDev + kisaVad, 2);

        var damga = Math.Round(genelToplamPek * options.DamgaVergisiOrani, 2);
        var maluliyetKisi = Math.Round(genelToplamPek * options.SgkMaluliyetKisiOrani, 2);
        var gssKisi = Math.Round(genelToplamPek * options.SgkGssKisiOrani, 2);
        var kesintiToplam = Math.Round(damga + maluliyetDev + gssDev + kisaVad + maluliyetKisi + gssKisi, 2);
        var eleGecen = Math.Round(gelirToplam - kesintiToplam, 2);

        return new Bordro4BResult
        {
            EmployeeId = payroll.Employee.Id,
            EmployeeName = payroll.Employee.FullName,
            EmployeeTitle = payroll.Employee.Title,
            UnitId = payroll.Employee.UnitId,
            UnitName = payroll.Employee.Unit?.Name,
            CalisilanBirimler = payroll.Employee.UnitId?.ToString(),
            Year = year,
            Month = month,
            NobetPuani = nobetPuani,
            YogunBakimVar = yogunBakimVar,
            SaatUcreti = saatUcreti,
            NormalServisNobetSaati = Round2(normalFazlaMesai),
            YogunBakimNobetSaati = Round2(yogunBakimFazlaMesai),
            NormalServisBayramSaati = Round2(normalBayramSaati),
            YogunBakimBayramSaati = Round2(yogunBakimBayramSaati),
            BayramFarkiNobetSaati = Round2(bayramFarkiSaati),
            NormalServisNobetToplamTutar = normalFazlaMesaiTutar,
            YogunBakimNobetToplamTutar = yogunBakimFazlaMesaiTutar,
            NormalServisBayramToplamTutar = normalBayramTutar,
            YogunBakimBayramToplamTutar = yogunBakimBayramTutar,
            BayramFarkiToplamTutar = bayramFarkiTutar,
            GenelToplamTutarPek = genelToplamPek,
            MaluliyetYaslilikEmeklilikDev = maluliyetDev,
            GssDev = gssDev,
            KisaVadSigKolPrim = kisaVad,
            GelirToplami = gelirToplam,
            DamgaVergisi = damga,
            MaluliyetYaslilikEmeklilikDevKesinti = maluliyetDev,
            GssDevKesinti = gssDev,
            KisaVadSigKolPrimKesinti = kisaVad,
            MaluliyetYaslilikEmeklilikKisi = maluliyetKisi,
            GssKisi = gssKisi,
            KesintiToplami = kesintiToplam,
            EleGecenToplam = eleGecen
        };
    }

    private static decimal Round2(decimal value)
    {
        return Math.Round(value, 2);
    }
}
