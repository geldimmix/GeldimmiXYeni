using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Helpers;
using Nobetci.Web.Models;

namespace Nobetci.Web.Services;

public interface IPuantajHesaplamaService
{
    Task<PuantajHesaplamaResult> HesaplaPuantaj(int unitId, int year, int month, bool mesaiSaatleriDahil, string hesaplayanTc);
    Task<PersonelPuantajDetay> HesaplaPersonelPuantaj(string personelTc, int unitId, int year, int month, bool mesaiSaatleriDahil);
    Task<List<PersonelPuantajOzet>> GetPersonelPuantajListesi(int unitId, int year, int month);
    Task<PuantajOzetBilgileri> GetPuantajOzet(int unitId, int year, int month);
}

public class PuantajHesaplamaService : IPuantajHesaplamaService
{
    private readonly IBordroHesaplamaService _bordroService;
    private readonly ApplicationDbContext _context;

    public PuantajHesaplamaService(IBordroHesaplamaService bordroService, ApplicationDbContext context)
    {
        _bordroService = bordroService;
        _context = context;
    }

    public async Task<PuantajHesaplamaResult> HesaplaPuantaj(int unitId, int year, int month, bool mesaiSaatleriDahil, string hesaplayanTc)
    {
        var payrolls = await _bordroService.GetEmployeePayrollsForUnit(unitId, year, month);
        if (!payrolls.Any())
        {
            return new PuantajHesaplamaResult
            {
                Basarili = false,
                HataMesaji = "Birimde aktif personel bulunamadı."
            };
        }

        var result = new PuantajHesaplamaResult
        {
            Basarili = true,
            HesaplananPersonelSayisi = payrolls.Count,
            ToplamPersonelSayisi = payrolls.Count,
            PersonelSayisi = payrolls.Count,
            Personeller = payrolls.Select(ToOzet).ToList(),
            Mesaj = "Puantaj hesaplama tamamlandı."
        };

        return result;
    }

    public async Task<PersonelPuantajDetay> HesaplaPersonelPuantaj(string personelTc, int unitId, int year, int month, bool mesaiSaatleriDahil)
    {
        var payroll = await _bordroService.GetEmployeePayrollForPersonel(personelTc, year, month);
        if (payroll == null)
        {
            return new PersonelPuantajDetay
            {
                TcKimlik = personelTc,
                Yil = year,
                Ay = month
            };
        }

        var organization = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == payroll.Employee.OrganizationId);
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == payroll.Employee.OrganizationId)
            .Where(h => h.Date.Year == year && h.Date.Month == month)
            .ToListAsync();
        var leaves = await _context.Leaves
            .Include(l => l.LeaveType)
            .Where(l => l.EmployeeId == payroll.Employee.Id)
            .Where(l => l.Date.Year == year && l.Date.Month == month)
            .ToListAsync();

        var weekendDays = (organization?.WeekendDays ?? "0,6").Split(',').Select(int.Parse).ToList();
        var detail = BuildPuantajDetay(payroll, year, month, holidays, weekendDays, leaves);
        return detail;
    }

    public async Task<List<PersonelPuantajOzet>> GetPersonelPuantajListesi(int unitId, int year, int month)
    {
        var payrolls = await _bordroService.GetEmployeePayrollsForUnit(unitId, year, month);
        return payrolls.Select(ToOzet).ToList();
    }

    public async Task<PuantajOzetBilgileri> GetPuantajOzet(int unitId, int year, int month)
    {
        var payrolls = await _bordroService.GetEmployeePayrollsForUnit(unitId, year, month);
        return new PuantajOzetBilgileri
        {
            PersonelSayisi = payrolls.Count,
            ToplamCalismaSaati = payrolls.Sum(p => p.TotalWorkedHours),
            ToplamFazlaMesai = payrolls.Sum(p => p.OvertimeHours),
            ToplamGeceMesaisi = payrolls.Sum(p => p.NightHours),
            ToplamTatilMesaisi = payrolls.Sum(p => p.HolidayHours),
            ToplamHaftaSonuMesaisi = payrolls.Sum(p => p.WeekendHours)
        };
    }

    private static PersonelPuantajOzet ToOzet(EmployeePayroll payroll)
    {
        return new PersonelPuantajOzet
        {
            TcKimlik = payroll.Employee.IdentityNo,
            AdSoyad = payroll.Employee.FullName,
            Unvan = payroll.Employee.Title,
            KadroTipi = payroll.Employee.PositionType,
            ToplamCalismaSaati = payroll.TotalWorkedHours,
            FazlaMesaiSaati = payroll.OvertimeHours
        };
    }

    private PersonelPuantajDetay BuildPuantajDetay(EmployeePayroll payroll, int year, int month, List<Holiday> holidays, List<int> weekendDays, List<Leave> leaves)
    {
        var gunlukDetaylar = BuildGunlukDetaylar(payroll, holidays, weekendDays, leaves);

        var normalFazlaMesai = payroll.NormalOvertimeHours;
        var yogunBakimFazlaMesai = payroll.IntensiveOvertimeHours;
        var normalBayram = payroll.NormalHolidayHours;
        var yogunBayram = payroll.IntensiveHolidayHours;

        var toplamBayramSaati = normalBayram + yogunBayram;
        var toplamFazlaMesai = normalFazlaMesai + yogunBakimFazlaMesai;
        var resmiTatilCalismaSaati = toplamBayramSaati > 0 ? Math.Min(toplamFazlaMesai, toplamBayramSaati) : 0;
        var bayramFarkiSaati = toplamBayramSaati > 0 ? Math.Max(0, toplamBayramSaati - toplamFazlaMesai) : 0;
        var yeniFazlaMesai = toplamBayramSaati > 0 ? Math.Max(0, toplamFazlaMesai - toplamBayramSaati) : toplamFazlaMesai;

        if (toplamFazlaMesai > 0 && yeniFazlaMesai != toplamFazlaMesai)
        {
            var normalOran = normalFazlaMesai / toplamFazlaMesai;
            var yogunOran = yogunBakimFazlaMesai / toplamFazlaMesai;
            normalFazlaMesai = yeniFazlaMesai * normalOran;
            yogunBakimFazlaMesai = yeniFazlaMesai * yogunOran;
        }

        var limitli = FazlaMesaiLimitHelper.LimitFazlaMesaiSaati(normalFazlaMesai, yogunBakimFazlaMesai);
        normalFazlaMesai = limitli.normalServis;
        yogunBakimFazlaMesai = limitli.yogunBakim;

        return new PersonelPuantajDetay
        {
            TcKimlik = payroll.Employee.IdentityNo,
            AdSoyad = payroll.Employee.FullName,
            Unvan = payroll.Employee.Title,
            KadroTipi = payroll.Employee.PositionType,
            Yil = year,
            Ay = month,
            ToplamCalismaSaati = payroll.TotalWorkedHours,
            HedefCalismaSaati = payroll.RequiredHours,
            FazlaMesaiSaati = normalFazlaMesai + yogunBakimFazlaMesai,
            NormalServisFazlaMesai = normalFazlaMesai,
            YogunBakimFazlaMesai = yogunBakimFazlaMesai,
            GeceCalismaSaati = payroll.NightHours,
            HaftaSonuSaati = payroll.WeekendHours,
            TatilSaati = resmiTatilCalismaSaati,
            NormalServisBayram = normalBayram,
            YogunBakimBayram = yogunBayram,
            BayramFarkiSaati = bayramFarkiSaati,
            BayramFarkiVar = bayramFarkiSaati > 0,
            YogunBakimVar = payroll.IsIntensiveCare,
            NobetGunSayisi = payroll.WorkedDays,
            IzinGunu = payroll.LeaveDays,
            YillikIzinGunu = payroll.AnnualLeaveDays,
            HastalikIzinGunu = payroll.SickLeaveDays,
            UlasimGunuSayisi = payroll.TransportationDays,
            GunlukDetaylar = gunlukDetaylar
        };
    }

    private List<PuantajGunlukDetay> BuildGunlukDetaylar(EmployeePayroll payroll, List<Holiday> holidays, List<int> weekendDays, List<Leave> leaves)
    {
        var results = new List<PuantajGunlukDetay>();
        var dayGroups = payroll.ShiftDetails.GroupBy(d => d.Date).OrderBy(g => g.Key);

        foreach (var group in dayGroups)
        {
            var date = group.Key;
            var totalHours = group.Sum(d => d.TotalHours);
            var nightHours = group.Sum(d => d.NightHours);
            var isHoliday = group.Any(d => d.IsHoliday);
            var isIntensive = group.Any(d => d.IsIntensiveCare);
            var isLeave = group.Any(d => d.IsLeave);
            var start = group.FirstOrDefault(d => d.StartTime.HasValue)?.StartTime;
            var end = group.FirstOrDefault(d => d.EndTime.HasValue)?.EndTime;

            var required = CalculateRequiredHoursForDate(payroll.Employee, date, holidays, weekendDays, leaves);
            var overtime = Math.Max(0, totalHours - required);
            var intensiveHours = group.Where(d => d.IsIntensiveCare).Sum(d => d.TotalHours);
            var normalOvertime = 0m;
            var intensiveOvertime = 0m;
            if (overtime > 0 && totalHours > 0)
            {
                var intensiveRatio = intensiveHours / totalHours;
                intensiveOvertime = overtime * intensiveRatio;
                normalOvertime = overtime - intensiveOvertime;
            }
            else if (overtime > 0)
            {
                normalOvertime = overtime;
            }

            var holidayHours = isHoliday ? totalHours : 0;
            var leave = leaves.FirstOrDefault(l => l.Date == date);

            results.Add(new PuantajGunlukDetay
            {
                Tarih = date,
                MesaiBaslangic = start,
                MesaiBitis = end,
                CalismaSaati = totalHours,
                NormalServisFazlaMesaiSaati = normalOvertime,
                YogunBakimFazlaMesaiSaati = intensiveOvertime,
                FazlaMesaiSaati = normalOvertime + intensiveOvertime,
                GeceCalismasiSaati = nightHours,
                BayramCalismasiSaati = holidayHours,
                BiletSayisi = CalculateTicketCount(start, end, payroll.Employee.HasDoubleTicketRight),
                ResmiTatilMi = isHoliday,
                YogunBakimMi = isIntensive,
                IzinliMi = isLeave,
                IzinTuru = leave?.LeaveType?.Code,
                Aciklama = leave?.Notes ?? group.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.Note))?.Note,
                CalisilanGunSayisi = totalHours > 0 ? 1 : 0
            });
        }

        return results;
    }

    private static decimal CalculateRequiredHoursForDate(Employee employee, DateOnly date, List<Holiday> holidays, List<int> weekendDays, List<Leave> leaves)
    {
        var isWeekend = weekendDays.Contains((int)date.DayOfWeek);
        var holiday = holidays.FirstOrDefault(h => h.Date == date);
        var leaveExists = leaves.Any(l => l.Date == date);

        if (leaveExists)
            return 0;

        if (holiday != null)
        {
            if (holiday.IsHalfDay && holiday.HalfDayWorkHours.HasValue)
                return holiday.HalfDayWorkHours.Value;
            return 0;
        }

        if (employee.WeekendWorkMode == 0 && isWeekend)
            return 0;

        if (employee.WeekendWorkMode == 2 && isWeekend && date.DayOfWeek != DayOfWeek.Saturday)
            return 0;

        if (date.DayOfWeek == DayOfWeek.Saturday && employee.SaturdayWorkHours.HasValue)
            return employee.SaturdayWorkHours.Value;

        return employee.DailyWorkHours;
    }

    private static int CalculateTicketCount(TimeOnly? start, TimeOnly? end, bool hasDoubleTicketRight)
    {
        if (!start.HasValue || !end.HasValue)
            return 0;

        var ticketCount = 0;
        if (start.Value != new TimeOnly(8, 0))
            ticketCount++;
        if (end.Value != new TimeOnly(17, 0))
            ticketCount++;
        if (hasDoubleTicketRight)
            ticketCount *= 2;
        return ticketCount;
    }
}
