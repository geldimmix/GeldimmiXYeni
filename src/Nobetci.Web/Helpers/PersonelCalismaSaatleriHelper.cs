using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Services;

namespace Nobetci.Web.Helpers;

public class PersonelCalismaSaatleriHelper
{
    private sealed class AkademikCalismaSaatiConfig
    {
        public double AsistanGunlukCalismaSaati { get; set; } = 8.0;
        public double OgretimUyesiGunlukCalismaSaati { get; set; } = 8.0;
        public double AsistanIzinSaati { get; set; } = 8.0;
        public double OgretimUyesiIzinSaati { get; set; } = 8.0;
    }

    private enum PersonelTipi
    {
        Memur,
        Isci
    }

    private const double MemurGunlukCalismaSaati = 8.0;
    private const double MemurYarimGunCalismaSaati = 4.0;
    private const double MemurIzinSaati = 8.0;
    private const double IsciGunlukCalismaSaati = 8.0;
    private const double IsciCumartesiCalismaSaati = 5.0;
    private const double IsciYarimGunCalismaSaati = 4.0;
    private const double IsciIzinSaati = 7.5;
    private const double RadyasyonGunlukCalismaSaati = 7.0;
    private const double SuaIzniSaati = 7.0;
    private readonly ApplicationDbContext _context;
    private readonly IBordroHesaplamaService _bordroService;
    private readonly IPuantajHesaplamaService _puantajService;
    private readonly AkademikCalismaSaatiConfig _akademikConfig = new();

    public PersonelCalismaSaatleriHelper(ApplicationDbContext context, IBordroHesaplamaService bordroService, IPuantajHesaplamaService puantajService)
    {
        _context = context;
        _bordroService = bordroService;
        _puantajService = puantajService;
    }

    public async Task<PersonelCalismaSonucu> HesaplaPersonelCalismaSaati(string tcKimlik, int yil, int ay)
    {
        var employee = await _context.Employees
            .Include(e => e.Unit)
            .ThenInclude(u => u.UnitType)
            .FirstOrDefaultAsync(e => e.IdentityNo == tcKimlik);
        if (employee == null)
        {
            return new PersonelCalismaSonucu
            {
                Basarili = false,
                HataMesaji = "Personel bilgileri bulunamadı.",
                PersonelTcKimlik = tcKimlik,
                Yil = yil,
                Ay = ay
            };
        }

        var payroll = await _bordroService.GetEmployeePayrollForPersonel(tcKimlik, yil, ay);
        if (payroll == null)
        {
            return new PersonelCalismaSonucu
            {
                Basarili = false,
                HataMesaji = "Puantaj hesaplanamadı.",
                PersonelTcKimlik = tcKimlik,
                PersonelAdSoyad = employee.FullName,
                KadroTipi = employee.PositionType,
                Yil = yil,
                Ay = ay
            };
        }

        var leaves = await _context.Leaves
            .Include(l => l.LeaveType)
            .Where(l => l.EmployeeId == employee.Id)
            .Where(l => l.Date.Year == yil && l.Date.Month == ay)
            .ToListAsync();

        var organization = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == employee.OrganizationId);
        var weekendDays = (organization?.WeekendDays ?? "0,6").Split(',').Select(int.Parse).ToList();
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == employee.OrganizationId)
            .Where(h => h.Date.Year == yil && h.Date.Month == ay)
            .ToListAsync();

        var monthDays = Enumerable.Range(1, DateTime.DaysInMonth(yil, ay))
            .Select(d => new DateOnly(yil, ay, d))
            .ToList();

        var fullDayHolidays = holidays.Where(h => !h.IsHalfDay).Select(h => h.Date).ToHashSet();
        var halfDayHolidays = holidays.Where(h => h.IsHalfDay).Select(h => h.Date).ToList();
        var haftaIciGunSayisi = monthDays.Count(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday);
        var haftaSonuGunSayisi = monthDays.Count(d => d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday);

        var isAkademik = string.Equals(employee.PositionType, "Academic", StringComparison.OrdinalIgnoreCase) ||
                         !string.IsNullOrWhiteSpace(employee.AcademicTitle);
        var isRadyasyon = IsRadyasyonUnit(employee.Unit);
        var personelTipi = ResolvePersonelTipi(employee);

        var plannedHours = CalculatePlannedHoursLegacy(
            employee,
            personelTipi,
            isAkademik,
            isRadyasyon,
            monthDays,
            fullDayHolidays,
            halfDayHolidays,
            leaves);
        if (isAkademik)
        {
            plannedHours = HesaplaAkademikCalismaSaati(employee.AcademicTitle, monthDays, fullDayHolidays, halfDayHolidays, leaves);
        }
        else if (isRadyasyon)
        {
            plannedHours = HesaplaRiskliGrupCalismaSaati(monthDays, fullDayHolidays, halfDayHolidays, leaves);
        }

        var izinGunleri = leaves.Select(l => l.Date).Distinct().ToList();
        var izinTurleri = leaves
            .Where(l => !string.IsNullOrWhiteSpace(l.LeaveType?.Code))
            .GroupBy(l => l.LeaveType!.Code)
            .ToDictionary(g => g.Key, g => g.Count());

        var nobetGunleri = payroll.ShiftDetails
            .Where(d => d.TotalHours > 0)
            .Select(d => new NobetBilgisi
            {
                Tarih = d.Date,
                BaslangicSaati = d.StartTime,
                BitisSaati = d.EndTime,
                CalismaSuresi = d.TotalHours,
                NobetTuttuguBirimId = employee.UnitId ?? -1,
                NobetTuttuguBirimAdi = employee.Unit?.Name,
                GorevYaptigiGrupTipi = d.IsIntensiveCare ? 3 : 1,
                GorevYaptigiGrupAdi = d.IsIntensiveCare ? "Yoğun Bakım" : "Normal"
            })
            .ToList();

        var birimAdi = employee.Unit?.Name ?? "Birim";
        var farkliBirimCalismaSaatleri = payroll.ShiftDetails
            .Where(d => d.TotalHours > 0)
            .GroupBy(_ => birimAdi)
            .ToDictionary(g => g.Key, g => (double)g.Sum(x => x.TotalHours));

        var farkliGrupCalismaSaatleri = payroll.ShiftDetails
            .Where(d => d.TotalHours > 0)
            .GroupBy(d => d.IsIntensiveCare ? "Yoğun Bakım" : "Normal")
            .ToDictionary(g => g.Key, g => (double)g.Sum(x => x.TotalHours));

        var gercekCalismaSaati = (double)payroll.TotalWorkedHours;
        var fazlaMesaiSaati = Math.Max(0, gercekCalismaSaati - plannedHours);

        return new PersonelCalismaSonucu
        {
            Basarili = true,
            PersonelTcKimlik = tcKimlik,
            PersonelAdSoyad = employee.FullName,
            KadroTipi = employee.PositionType,
            IsAkademik = isAkademik,
            AkademikUnvan = employee.AcademicTitle,
            Yil = yil,
            Ay = ay,
            HaftaIciGunSayisi = haftaIciGunSayisi,
            HaftaSonuGunSayisi = haftaSonuGunSayisi,
            ResmiTatilGunSayisi = fullDayHolidays.Count,
            YarimGunTatilSayisi = halfDayHolidays.Count,
            RadyasyonAlanindaMi = isRadyasyon,
            CalismaSaati = plannedHours,
            GercekCalismaSaati = gercekCalismaSaati,
            FazlaMesaiSaati = fazlaMesaiSaati,
            IzinGunleri = izinGunleri,
            IzinTurleri = izinTurleri,
            NobetGunleri = nobetGunleri,
            FarkliBirimCalismaSaatleri = farkliBirimCalismaSaatleri,
            FarkliGrupCalismaSaatleri = farkliGrupCalismaSaatleri
        };
    }

    public async Task<PersonelPuantajHesaplamaSonucu> HesaplaPersonelPuantaj(string tcKimlik, int yil, int ay)
    {
        var payroll = await _bordroService.GetEmployeePayrollForPersonel(tcKimlik, yil, ay);
        if (payroll == null)
        {
            return new PersonelPuantajHesaplamaSonucu
            {
                Basarili = false,
                HataMesaji = "Puantaj hesaplanamadı.",
                PersonelTcKimlik = tcKimlik,
                Yil = yil,
                Ay = ay
            };
        }

        var detay = await _puantajService.HesaplaPersonelPuantaj(tcKimlik, payroll.Employee.UnitId ?? 0, yil, ay, false);
        return new PersonelPuantajHesaplamaSonucu
        {
            Basarili = true,
            PersonelTcKimlik = detay.TcKimlik,
            PersonelAdSoyad = detay.AdSoyad,
            PersonelUnvan = detay.Unvan,
            KadroTipi = detay.KadroTipi,
            DetayliKadroTipi = detay.KadroTipi,
            MantiksalBirimId = payroll.Employee.UnitId ?? 0,
            BirimAdi = payroll.Employee.Unit?.Name,
            Yil = detay.Yil,
            Ay = detay.Ay,
            ToplamCalismaSaati = (double)detay.ToplamCalismaSaati,
            PlanlananCalismaSaati = (double)detay.HedefCalismaSaati,
            FazlaMesaiSaati = (double)detay.FazlaMesaiSaati,
            NormalServisFazlaMesai = (double)detay.NormalServisFazlaMesai,
            YogunBakimFazlaMesai = (double)detay.YogunBakimFazlaMesai,
            GeceCalismaSaati = (double)detay.GeceCalismaSaati,
            HaftasonuCalismaSaati = (double)detay.HaftaSonuSaati,
            ResmiTatilCalismaSaati = (double)detay.TatilSaati,
            NormalServisBayram = (double)detay.NormalServisBayram,
            YogunBakimBayram = (double)detay.YogunBakimBayram,
            BayramCalismasiSaati = (double)(detay.NormalServisBayram + detay.YogunBakimBayram),
            BayramNobetFarkiSaati = (double)detay.BayramFarkiSaati,
            BayramFarkiVar = detay.BayramFarkiVar,
            YogunBakimVar = detay.YogunBakimVar,
            NobetGunSayisi = detay.NobetGunSayisi,
            IzinGunSayisi = detay.IzinGunu,
            YillikIzinGunSayisi = detay.YillikIzinGunu,
            HastalikIzinGunSayisi = detay.HastalikIzinGunu,
            UlasimGunuSayisi = detay.UlasimGunuSayisi,
            CalistigiGunSayisi = detay.GunlukDetaylar.Count(d => d.CalisilanGunSayisi > 0),
            BiletSayisi = detay.GunlukDetaylar.Sum(d => d.BiletSayisi),
            YogunBakimMi = detay.YogunBakimVar,
            GunlukDetaylar = detay.GunlukDetaylar
        };
    }

    private static double HesaplaAkademikCalismaSaati(string? akademikUnvan, List<DateOnly> gunler, HashSet<DateOnly> tamGunTatiller, List<DateOnly> yarimGunTatiller, List<Leave> izinler)
    {
        var gunlukCalismaSaati = akademikUnvan?.Contains("Araştırma", StringComparison.OrdinalIgnoreCase) == true
            ? MemurGunlukCalismaSaati
            : MemurGunlukCalismaSaati;
        var izinSaati = MemurIzinSaati;

        var ucretsizIzinGunleri = izinler
            .Where(i => string.Equals(i.LeaveType?.Category, "unpaid", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Date)
            .Distinct()
            .ToList();

        var calismaGunleri = gunler.Where(g =>
            g.DayOfWeek != DayOfWeek.Saturday &&
            g.DayOfWeek != DayOfWeek.Sunday &&
            !tamGunTatiller.Contains(g) &&
            !ucretsizIzinGunleri.Contains(g)).Count();

        var yarimGunTatilSayisi = yarimGunTatiller.Count(t =>
            t.DayOfWeek != DayOfWeek.Saturday &&
            t.DayOfWeek != DayOfWeek.Sunday &&
            !ucretsizIzinGunleri.Contains(t));

        calismaGunleri -= yarimGunTatilSayisi;
        var toplam = calismaGunleri * gunlukCalismaSaati;
        toplam += yarimGunTatilSayisi * MemurYarimGunCalismaSaati;

        var normalIzinGunleri = izinler
            .Where(i => i.Date.DayOfWeek != DayOfWeek.Saturday && i.Date.DayOfWeek != DayOfWeek.Sunday)
            .Select(i => i.Date)
            .Distinct()
            .Count(d => !tamGunTatiller.Contains(d) && !yarimGunTatiller.Contains(d));

        var yarimGunTatildeIzinli = izinler
            .Select(i => i.Date)
            .Distinct()
            .Count(d => yarimGunTatiller.Contains(d));

        var izinDusulmesi = normalIzinGunleri * izinSaati;
        izinDusulmesi += yarimGunTatildeIzinli * MemurYarimGunCalismaSaati;
        toplam = Math.Max(0, toplam - izinDusulmesi);

        return toplam;
    }

    private static double HesaplaRiskliGrupCalismaSaati(List<DateOnly> gunler, HashSet<DateOnly> tamGunTatiller, List<DateOnly> yarimGunTatiller, List<Leave> izinler)
    {
        var ucretsizIzinGunleri = izinler
            .Where(i => string.Equals(i.LeaveType?.Category, "unpaid", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Date)
            .Distinct()
            .ToList();

        var calismaGunleri = gunler.Where(g =>
            g.DayOfWeek != DayOfWeek.Saturday &&
            g.DayOfWeek != DayOfWeek.Sunday &&
            !tamGunTatiller.Contains(g) &&
            !ucretsizIzinGunleri.Contains(g)).Count();

        var yarimGunTatilSayisi = yarimGunTatiller.Count(t =>
            t.DayOfWeek != DayOfWeek.Saturday &&
            t.DayOfWeek != DayOfWeek.Sunday &&
            !ucretsizIzinGunleri.Contains(t));

        calismaGunleri -= yarimGunTatilSayisi;
        var toplam = calismaGunleri * RadyasyonGunlukCalismaSaati;
        toplam += yarimGunTatilSayisi * MemurYarimGunCalismaSaati;

        var izinGunleri = izinler
            .Where(i => i.Date.DayOfWeek != DayOfWeek.Saturday && i.Date.DayOfWeek != DayOfWeek.Sunday)
            .Select(i => i.Date)
            .Distinct()
            .Count(d => !tamGunTatiller.Contains(d) && !yarimGunTatiller.Contains(d));

        toplam = Math.Max(0, toplam - izinGunleri * SuaIzniSaati);
        return toplam;
    }

    private double CalculatePlannedHoursLegacy(
        Employee employee,
        PersonelTipi personelTipi,
        bool isAkademik,
        bool isRadyasyon,
        List<DateOnly> gunler,
        HashSet<DateOnly> tamGunTatiller,
        List<DateOnly> yarimGunTatiller,
        List<Leave> izinler)
    {
        var ucretsizIzinGunleri = izinler
            .Where(i => string.Equals(i.LeaveType?.Category, "unpaid", StringComparison.OrdinalIgnoreCase) ||
                        i.LeaveType?.Code is "Ü.İ" or "Ücretsiz" or "Ücretsiz İzin")
            .Select(i => i.Date)
            .Distinct()
            .ToList();

        var haftaIciGunler = gunler.Count(g => g.DayOfWeek != DayOfWeek.Saturday && g.DayOfWeek != DayOfWeek.Sunday);
        var tamGunTatilHaftaIci = tamGunTatiller.Count(t =>
            t.DayOfWeek != DayOfWeek.Saturday && t.DayOfWeek != DayOfWeek.Sunday);

        var yarimGunTatilSayisi = yarimGunTatiller.Count(t =>
            t.DayOfWeek != DayOfWeek.Saturday &&
            t.DayOfWeek != DayOfWeek.Sunday &&
            !ucretsizIzinGunleri.Contains(t));

        var haftaIciCalismaGunleri = haftaIciGunler - tamGunTatilHaftaIci - yarimGunTatilSayisi;
        var cumartesiGunleri = gunler.Count(g => g.DayOfWeek == DayOfWeek.Saturday);
        var cumartesiCalismaGunleri = cumartesiGunleri - tamGunTatiller.Count(t => t.DayOfWeek == DayOfWeek.Saturday);

        double calismaSaati;
        if (isAkademik)
        {
            calismaSaati = HesaplaAkademikCalismaSaati(employee.AcademicTitle, gunler, tamGunTatiller, yarimGunTatiller, izinler);
        }
        else if (isRadyasyon)
        {
            calismaSaati = HesaplaRiskliGrupCalismaSaati(gunler, tamGunTatiller, yarimGunTatiller, izinler);
        }
        else
        {
            switch (personelTipi)
            {
                case PersonelTipi.Isci:
                    calismaSaati = haftaIciCalismaGunleri * IsciGunlukCalismaSaati;
                    calismaSaati += cumartesiCalismaGunleri * IsciCumartesiCalismaSaati;
                    calismaSaati += yarimGunTatilSayisi * IsciYarimGunCalismaSaati;
                    break;
                default:
                    calismaSaati = haftaIciCalismaGunleri * MemurGunlukCalismaSaati;
                    calismaSaati += yarimGunTatilSayisi * MemurYarimGunCalismaSaati;
                    break;
            }
        }

        if (ucretsizIzinGunleri.Count > 0)
        {
            var ucretsizHaftaIci = ucretsizIzinGunleri.Count(g =>
                g.DayOfWeek != DayOfWeek.Saturday && g.DayOfWeek != DayOfWeek.Sunday && !tamGunTatiller.Contains(g));
            var ucretsizCumartesi = ucretsizIzinGunleri.Count(g =>
                g.DayOfWeek == DayOfWeek.Saturday && !tamGunTatiller.Contains(g));

            if (personelTipi == PersonelTipi.Isci)
            {
                calismaSaati -= ucretsizHaftaIci * IsciGunlukCalismaSaati;
                calismaSaati -= ucretsizCumartesi * IsciCumartesiCalismaSaati;
            }
            else
            {
                calismaSaati -= ucretsizHaftaIci * MemurGunlukCalismaSaati;
            }
        }

        var normalIzinGunleri = izinler
            .Where(i => !string.Equals(i.LeaveType?.Category, "unpaid", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Date)
            .Distinct()
            .ToList();

        var yarimGunTatildeIzinli = normalIzinGunleri
            .Where(d => yarimGunTatiller.Contains(d) && d.DayOfWeek != DayOfWeek.Sunday)
            .ToList();

        var haftaIciIzinGunleri = normalIzinGunleri.Count(d =>
            d.DayOfWeek != DayOfWeek.Saturday &&
            d.DayOfWeek != DayOfWeek.Sunday &&
            !tamGunTatiller.Contains(d) &&
            !yarimGunTatildeIzinli.Contains(d));

        var cumartesiIzinGunleri = normalIzinGunleri.Count(d =>
            d.DayOfWeek == DayOfWeek.Saturday && !tamGunTatiller.Contains(d));

        double izinGunleriSaat;
        if (isRadyasyon)
        {
            izinGunleriSaat = haftaIciIzinGunleri * RadyasyonGunlukCalismaSaati;
        }
        else if (personelTipi == PersonelTipi.Isci)
        {
            izinGunleriSaat = (haftaIciIzinGunleri + cumartesiIzinGunleri) * IsciIzinSaati;
        }
        else
        {
            izinGunleriSaat = haftaIciIzinGunleri * MemurIzinSaati;
        }

        izinGunleriSaat += yarimGunTatildeIzinli.Count * MemurYarimGunCalismaSaati;

        return Math.Max(0, calismaSaati - izinGunleriSaat);
    }

    private static PersonelTipi ResolvePersonelTipi(Employee employee)
    {
        if (string.Equals(employee.PositionType, "4D", StringComparison.OrdinalIgnoreCase))
            return PersonelTipi.Isci;

        if (!string.IsNullOrWhiteSpace(employee.Title) &&
            employee.Title.Contains("İşçi", StringComparison.OrdinalIgnoreCase))
            return PersonelTipi.Isci;

        return PersonelTipi.Memur;
    }

    private static bool IsRadyasyonUnit(Unit? unit)
    {
        var unitTypeName = unit?.UnitType?.Name ?? unit?.Name ?? string.Empty;
        return unitTypeName.Contains("radyasyon", StringComparison.OrdinalIgnoreCase);
    }
}
