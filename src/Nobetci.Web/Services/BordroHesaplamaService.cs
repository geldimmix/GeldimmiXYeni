using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Helpers;
using Nobetci.Web.Models;

namespace Nobetci.Web.Services;

public interface IBordroHesaplamaService
{
    Task<BordroHesaplamaOzet> HesaplaBirimBordrolari(int unitId, int year, int month, string hesaplayanTc, bool yenidenHesapla = false);
    Task<Bordro4AResult?> Hesapla4ABordro(string personelTc, int year, int month, bool yenidenHesapla = false);
    Task<Bordro4BResult?> Hesapla4BBordro(string personelTc, int year, int month, bool yenidenHesapla = false);
    Task<Bordro4AResult?> Get4ABordroDetay(string personelTc, int year, int month);
    Task<Bordro4BResult?> Get4BBordroDetay(string personelTc, int year, int month);
    Task<List<Bordro4AResult>> GetBirim4ABordrolari(int unitId, int year, int month);
    Task<List<Bordro4BResult>> GetBirim4BBordrolari(int unitId, int year, int month);
    Task<List<BordroSabitleri>> GetAktifSabitler(int organizationId, string? cadreType = null);
    Task<bool> SabitEkleVeyaGuncelle(int organizationId, BordroSabitInputModel model, string actionBy);
    Task<BordroDetayViewModel> GetBordroDetayWithSteps(string personelTc, int year, int month);
    Task<List<EmployeePayroll>> GetEmployeePayrollsForUnit(int unitId, int year, int month);
    Task<EmployeePayroll?> GetEmployeePayrollForPersonel(string personelTc, int year, int month);
    Task EnsureBordroSabitleriAsync(int organizationId);
    Task SyncBordroSabitleriFromTemplatesAsync(int organizationId);
    /// <summary>Ensure org has default unit types (from templates). Used when user opens Bordro/Sabitler or app without visiting Index first.</summary>
    Task EnsureDefaultUnitTypesAsync(int organizationId);
    /// <summary>Ensure org has at least one unit (e.g. "Genel Birim"). Call after EnsureDefaultUnitTypesAsync.</summary>
    Task EnsureDefaultUnitAsync(int organizationId);
}

public class BordroHesaplamaService : IBordroHesaplamaService
{
    private readonly ApplicationDbContext _context;
    private readonly IBordroCalculator _bordroCalculator;
    private readonly IConfiguration _configuration;

    public BordroHesaplamaService(ApplicationDbContext context, IBordroCalculator bordroCalculator, IConfiguration configuration)
    {
        _context = context;
        _bordroCalculator = bordroCalculator;
        _configuration = configuration;
    }

    public async Task<BordroHesaplamaOzet> HesaplaBirimBordrolari(int unitId, int year, int month, string hesaplayanTc, bool yenidenHesapla = false)
    {
        var unit = await _context.Units.Include(u => u.UnitType).FirstOrDefaultAsync(u => u.Id == unitId);
        if (unit == null)
            throw new Exception("Birim bulunamadı");

        var organization = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == unit.OrganizationId);
        if (organization == null)
            throw new Exception("Organizasyon bulunamadı");

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.UnitId == unitId && e.IsActive)
            .ToListAsync();
        if (!employees.Any())
            throw new Exception("Birimde aktif personel bulunamadı");

        var units = await _context.Units
            .Include(u => u.UnitType)
            .Where(u => u.OrganizationId == organization.Id && u.IsActive)
            .ToListAsync();

        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id)
            .Where(h => h.Date.Year == year && h.Date.Month == month)
            .ToListAsync();

        var leaves = await _context.Leaves
            .Include(l => l.LeaveType)
            .Where(l => l.Employee.OrganizationId == organization.Id)
            .Where(l => l.Date.Year == year && l.Date.Month == month)
            .ToListAsync();

        var nightStart = organization.NightStartTime;
        var nightEnd = organization.NightEndTime;

        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .ToListAsync();

        var prevMonth = new DateOnly(year, month, 1).AddDays(-1);
        var prevMonthShifts = await _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date == prevMonth && s.SpansNextDay)
            .ToListAsync();

        var payrollOptions = GetPayrollOptions();
        var bordroOptions = await GetBordroOptionsAsync(organization.Id);
        var employeePayrolls = CalculateEmployeePayrolls(
            employees,
            shifts,
            prevMonthShifts,
            holidays,
            leaves,
            organization,
            year,
            month,
            nightStart,
            nightEnd,
            payrollOptions,
            units);

        var puanMap = await GetPersonelPuanMapAsync(organization.Id);
        var bordro4A = _bordroCalculator.Calculate4A(employeePayrolls, bordroOptions, year, month, puanMap);
        var bordro4B = _bordroCalculator.Calculate4B(employeePayrolls, bordroOptions, year, month, puanMap);

        await SaveBordroResultsAsync(organization.Id, year, month, bordro4A, bordro4B, employees.Select(e => e.Id).ToList());

        return new BordroHesaplamaOzet
        {
            MantiksalBirimId = unitId,
            BirimAdi = unit.Name,
            Yil = year,
            Ay = month,
            AyAdi = GetAyAdi(month),
            Bordro4APersonelSayisi = bordro4A.Select(b => b.EmployeeId).Distinct().Count(),
            Bordro4BPersonelSayisi = bordro4B.Select(b => b.EmployeeId).Distinct().Count(),
            Bordro4AToplamTutar = bordro4A.Sum(b => b.EleGecenToplam),
            Bordro4BToplamTutar = bordro4B.Sum(b => b.EleGecenToplam),
            BasarisizPersonelListesi = new List<string>()
        };
    }

    public async Task<Bordro4AResult?> Hesapla4ABordro(string personelTc, int year, int month, bool yenidenHesapla = false)
    {
        var bordro = await CalculatePersonelBordro(personelTc, year, month);
        return bordro.bordro4A;
    }

    public async Task<Bordro4BResult?> Hesapla4BBordro(string personelTc, int year, int month, bool yenidenHesapla = false)
    {
        var bordro = await CalculatePersonelBordro(personelTc, year, month);
        return bordro.bordro4B;
    }

    public async Task<Bordro4AResult?> Get4ABordroDetay(string personelTc, int year, int month)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityNo == personelTc);
        if (employee == null)
            return null;

        var results = await _context.BordroResults4A
            .Where(b => b.OrganizationId == employee.OrganizationId && b.EmployeeId == employee.Id && b.Year == year && b.Month == month)
            .ToListAsync();
        if (!results.Any())
            return null;

        return Aggregate4A(results, employee);
    }

    public async Task<Bordro4BResult?> Get4BBordroDetay(string personelTc, int year, int month)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityNo == personelTc);
        if (employee == null)
            return null;

        var results = await _context.BordroResults4B
            .Where(b => b.OrganizationId == employee.OrganizationId && b.EmployeeId == employee.Id && b.Year == year && b.Month == month)
            .ToListAsync();
        if (!results.Any())
            return null;

        return Aggregate4B(results, employee);
    }

    public async Task<List<Bordro4AResult>> GetBirim4ABordrolari(int unitId, int year, int month)
    {
        var employees = await _context.Employees
            .Where(e => e.UnitId == unitId && e.IsActive)
            .Select(e => e.Id)
            .ToListAsync();

        var items = await _context.BordroResults4A
            .Where(b => employees.Contains(b.EmployeeId) && b.Year == year && b.Month == month)
            .ToListAsync();

        return await Project4A(items);
    }

    public async Task<List<Bordro4BResult>> GetBirim4BBordrolari(int unitId, int year, int month)
    {
        var employees = await _context.Employees
            .Where(e => e.UnitId == unitId && e.IsActive)
            .Select(e => e.Id)
            .ToListAsync();

        var items = await _context.BordroResults4B
            .Where(b => employees.Contains(b.EmployeeId) && b.Year == year && b.Month == month)
            .ToListAsync();

        return await Project4B(items);
    }

    public async Task<List<BordroSabitleri>> GetAktifSabitler(int organizationId, string? cadreType = null)
    {
        await EnsureBordroSabitleriAsync(organizationId);

        var query = _context.BordroSabitleri
            .Where(s => s.OrganizationId == organizationId && s.IsActive);
        if (!string.IsNullOrWhiteSpace(cadreType))
            query = query.Where(s => s.CadreType == cadreType);
        return await query.OrderBy(s => s.Key).ToListAsync();
    }

    public async Task<bool> SabitEkleVeyaGuncelle(int organizationId, BordroSabitInputModel model, string actionBy)
    {
        var existing = await _context.BordroSabitleri
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Key == model.Key);
        if (existing == null)
        {
            _context.BordroSabitleri.Add(new BordroSabitleri
            {
                OrganizationId = organizationId,
                Key = model.Key,
                Value = model.Value,
                ValueType = model.ValueType,
                Description = model.Description,
                CadreType = model.CadreType,
                ValidFrom = model.ValidFrom,
                ValidTo = model.ValidTo,
                IsActive = model.IsActive,
                WorkingUnitIds = model.WorkingUnitIds,
                CreatedBy = actionBy,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return true;
        }

        _context.BordroSabitleriGecmis.Add(new BordroSabitleriGecmis
        {
            OrganizationId = existing.OrganizationId,
            SabitId = existing.Id,
            Key = existing.Key,
            OldValue = existing.Value,
            NewValue = model.Value,
            ValueType = existing.ValueType,
            Description = existing.Description,
            CadreType = existing.CadreType,
            OldValidFrom = existing.ValidFrom,
            OldValidTo = existing.ValidTo,
            NewValidFrom = model.ValidFrom,
            NewValidTo = model.ValidTo,
            OldWorkingUnitIds = existing.WorkingUnitIds,
            NewWorkingUnitIds = model.WorkingUnitIds,
            ActionType = "UPDATE",
            ActionBy = actionBy
        });

        existing.Value = model.Value;
        existing.ValueType = model.ValueType;
        existing.Description = model.Description;
        existing.CadreType = model.CadreType;
        existing.ValidFrom = model.ValidFrom;
        existing.ValidTo = model.ValidTo;
        existing.IsActive = model.IsActive;
        existing.WorkingUnitIds = model.WorkingUnitIds;
        existing.UpdatedBy = actionBy;
        existing.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<BordroDetayViewModel> GetBordroDetayWithSteps(string personelTc, int year, int month)
    {
        var bordro4A = await Get4ABordroDetay(personelTc, year, month);
        var bordro4B = await Get4BBordroDetay(personelTc, year, month);

        var steps = new List<string>();
        if (bordro4A != null)
        {
            steps.Add($"Saat Ücreti: {bordro4A.SaatUcreti:0.##}");
            steps.Add($"Genel Toplam: {bordro4A.GenelToplamTutar:0.##}");
            steps.Add($"Damga Vergisi: {bordro4A.DamgaVergisi:0.##}");
            steps.Add($"Net: {bordro4A.EleGecenToplam:0.##}");
        }
        if (bordro4B != null)
        {
            steps.Add($"PEK: {bordro4B.GenelToplamTutarPek:0.##}");
            steps.Add($"Gelir Toplamı: {bordro4B.GelirToplami:0.##}");
            steps.Add($"Kesinti Toplamı: {bordro4B.KesintiToplami:0.##}");
            steps.Add($"Net: {bordro4B.EleGecenToplam:0.##}");
        }

        return new BordroDetayViewModel
        {
            Bordro4A = bordro4A,
            Bordro4B = bordro4B,
            Steps = steps
        };
    }

    public async Task<List<EmployeePayroll>> GetEmployeePayrollsForUnit(int unitId, int year, int month)
    {
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == unitId);
        if (unit == null)
            return new List<EmployeePayroll>();

        return await BuildEmployeePayrolls(unit.OrganizationId, unitId, year, month);
    }

    public async Task<EmployeePayroll?> GetEmployeePayrollForPersonel(string personelTc, int year, int month)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.IdentityNo == personelTc);
        if (employee == null)
            return null;

        var payrolls = await BuildEmployeePayrolls(employee.OrganizationId, employee.UnitId, year, month, new List<int> { employee.Id });
        return payrolls.FirstOrDefault();
    }

    public async Task EnsureBordroSabitleriAsync(int organizationId)
    {
        var templates = await _context.BordroSabitleriTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Key)
            .ToListAsync();

        if (!templates.Any())
        {
            templates = BuildDefaultBordroTemplates();
            _context.BordroSabitleriTemplates.AddRange(templates);
            await _context.SaveChangesAsync();
        }

        var hasOrgSabitler = await _context.BordroSabitleri
            .AnyAsync(s => s.OrganizationId == organizationId);
        if (hasOrgSabitler)
        {
            var configured = new BordroOptions();
            _configuration.GetSection("Bordro").Bind(configured);
            var targetDamga = Math.Round(configured.DamgaVergisiOrani, 5, MidpointRounding.AwayFromZero);
            if (targetDamga > 0)
            {
                var existingDamga = await _context.BordroSabitleri
                    .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.Key == "DAMGA_VERGISI_ORANI");
                if (existingDamga != null && Math.Round(existingDamga.Value, 5, MidpointRounding.AwayFromZero) != targetDamga)
                {
                    existingDamga.Value = targetDamga;
                    existingDamga.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                    existingDamga.UpdatedBy = "system";
                    await _context.SaveChangesAsync();
                }
            }

            return;
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
        var orgItems = templates.Select(t => new BordroSabitleri
        {
            OrganizationId = organizationId,
            Key = t.Key,
            Value = t.Value,
            ValueType = t.ValueType,
            Description = t.Description,
            CadreType = t.CadreType,
            ValidFrom = now,
            ValidTo = null,
            IsActive = t.IsActive,
            WorkingUnitIds = t.WorkingUnitIds,
            TemplateId = t.Id,
            IsCustom = false,
            CreatedBy = t.CreatedBy ?? "system",
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        }).ToList();

        _context.BordroSabitleri.AddRange(orgItems);
        await _context.SaveChangesAsync();
    }

    public async Task SyncBordroSabitleriFromTemplatesAsync(int organizationId)
    {
        var templates = await _context.BordroSabitleriTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Key)
            .ToListAsync();

        if (!templates.Any())
            return;

        await SyncBordroSabitleriFromTemplatesAsync(organizationId, templates);
    }

    public async Task EnsureDefaultUnitTypesAsync(int organizationId)
    {
        await EnsureUnitTypeTemplatesAsync();
        var existingTypes = await _context.UnitTypes.AnyAsync(ut => ut.OrganizationId == organizationId);
        if (existingTypes)
            return;
        var templates = await _context.UnitTypeTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
        var defaultTypes = templates.Select(t => new UnitType
        {
            OrganizationId = organizationId,
            Name = t.Name,
            NameEn = t.NameEn,
            DefaultCoefficient = t.DefaultCoefficient,
            Color = t.Color,
            Icon = t.Icon,
            SortOrder = t.SortOrder,
            IsActive = true,
            IsSystem = true,
            TemplateId = t.Id,
            IsCustom = false
        }).ToList();
        _context.UnitTypes.AddRange(defaultTypes);
        await _context.SaveChangesAsync();
    }

    public async Task EnsureDefaultUnitAsync(int organizationId)
    {
        var existingUnits = await _context.Units.AnyAsync(u => u.OrganizationId == organizationId);
        if (existingUnits)
            return;
        var defaultType = await _context.UnitTypes
            .Where(ut => ut.OrganizationId == organizationId)
            .OrderBy(ut => ut.SortOrder)
            .FirstOrDefaultAsync();
        var defaultUnit = new Unit
        {
            OrganizationId = organizationId,
            UnitTypeId = defaultType?.Id,
            Name = "Genel Birim",
            Description = "Varsayılan birim - tüm personel burada başlar",
            Coefficient = 1.0m,
            Color = "#3B82F6",
            IsDefault = true,
            IsActive = true,
            SortOrder = 1
        };
        _context.Units.Add(defaultUnit);
        await _context.SaveChangesAsync();
        var employeesWithoutUnit = await _context.Employees
            .Where(e => e.OrganizationId == organizationId && e.UnitId == null && e.IsActive)
            .ToListAsync();
        foreach (var emp in employeesWithoutUnit)
            emp.UnitId = defaultUnit.Id;
        if (employeesWithoutUnit.Any())
            await _context.SaveChangesAsync();
    }

    private async Task EnsureUnitTypeTemplatesAsync()
    {
        if (await _context.UnitTypeTemplates.AnyAsync())
            return;
        var systemTypes = await _context.UnitTypes
            .Where(ut => ut.IsSystem)
            .OrderBy(ut => ut.SortOrder)
            .ToListAsync();
        List<UnitTypeTemplate> templates;
        if (systemTypes.Any())
        {
            templates = systemTypes.Select(t => new UnitTypeTemplate
            {
                Name = t.Name,
                NameEn = t.NameEn,
                DefaultCoefficient = t.DefaultCoefficient,
                Color = t.Color,
                Icon = t.Icon,
                SortOrder = t.SortOrder,
                IsActive = true
            }).ToList();
        }
        else
        {
            templates = new List<UnitTypeTemplate>
            {
                new UnitTypeTemplate { Name = "Poliklinik/Servis", NameEn = "Polyclinic/Service", DefaultCoefficient = 1.0m, Color = "#3B82F6", Icon = "hospital", SortOrder = 1, IsActive = true },
                new UnitTypeTemplate { Name = "Yoğun Bakım", NameEn = "Intensive Care Unit", DefaultCoefficient = 1.5m, Color = "#EF4444", Icon = "heart-pulse", SortOrder = 2, IsActive = true },
                new UnitTypeTemplate { Name = "Radyasyon Birimi", NameEn = "Radiation Unit", DefaultCoefficient = 1.5m, Color = "#F59E0B", Icon = "radiation", SortOrder = 3, IsActive = true }
            };
        }
        _context.UnitTypeTemplates.AddRange(templates);
        await _context.SaveChangesAsync();
    }

    private async Task SyncBordroSabitleriFromTemplatesAsync(int organizationId, List<BordroSabitleriTemplate> templates)
    {
        var orgItems = await _context.BordroSabitleri
            .Where(s => s.OrganizationId == organizationId)
            .ToListAsync();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var updated = false;

        foreach (var template in templates)
        {
            var match = orgItems.FirstOrDefault(s => s.TemplateId == template.Id);
            if (match == null)
            {
                match = orgItems.FirstOrDefault(s =>
                    s.TemplateId == null &&
                    string.Equals(s.Key, template.Key, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.CadreType ?? string.Empty, template.CadreType ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            }

            if (match == null)
            {
                _context.BordroSabitleri.Add(new BordroSabitleri
                {
                    OrganizationId = organizationId,
                    Key = template.Key,
                    Value = template.Value,
                    ValueType = template.ValueType,
                    Description = template.Description,
                    CadreType = template.CadreType,
                    ValidFrom = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified),
                    ValidTo = null,
                    IsActive = template.IsActive,
                    WorkingUnitIds = template.WorkingUnitIds,
                    TemplateId = template.Id,
                    IsCustom = false,
                    CreatedBy = template.CreatedBy ?? "system",
                    CreatedAt = now
                });
                updated = true;
                continue;
            }

            if (match.TemplateId == null)
            {
                match.TemplateId = template.Id;
                updated = true;
            }

            if (!match.IsCustom)
            {
                match.Value = template.Value;
                match.ValueType = template.ValueType;
                match.Description = template.Description;
                match.CadreType = template.CadreType;
                match.WorkingUnitIds = template.WorkingUnitIds;
                match.IsActive = template.IsActive;
                match.UpdatedBy = "system";
                match.UpdatedAt = now;
                updated = true;
            }
        }

        if (updated)
            await _context.SaveChangesAsync();
    }

    private async Task<(Bordro4AResult? bordro4A, Bordro4BResult? bordro4B)> CalculatePersonelBordro(string personelTc, int year, int month)
    {
        var employee = await _context.Employees
            .Include(e => e.Unit)
            .ThenInclude(u => u.UnitType)
            .FirstOrDefaultAsync(e => e.IdentityNo == personelTc && e.IsActive);
        if (employee == null)
            return (null, null);

        var organization = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == employee.OrganizationId);
        if (organization == null)
            return (null, null);

        var payrolls = await BuildEmployeePayrolls(organization.Id, employee.UnitId, year, month, new List<int> { employee.Id });
        var bordroOptions = await GetBordroOptionsAsync(organization.Id, employee.PositionType);

        var puanMap = await GetPersonelPuanMapAsync(organization.Id);
        var bordro4A = _bordroCalculator.Calculate4A(payrolls, bordroOptions, year, month, puanMap).FirstOrDefault();
        var bordro4B = _bordroCalculator.Calculate4B(payrolls, bordroOptions, year, month, puanMap).FirstOrDefault();

        if (bordro4A != null || bordro4B != null)
        {
            await SaveBordroResultsAsync(organization.Id, year, month,
                bordro4A != null ? new List<Bordro4AResult> { bordro4A } : new(),
                bordro4B != null ? new List<Bordro4BResult> { bordro4B } : new(),
                new List<int> { employee.Id });
        }

        return (bordro4A, bordro4B);
    }

    private async Task<List<EmployeePayroll>> BuildEmployeePayrolls(int organizationId, int? unitId, int year, int month, List<int>? employeeIds = null)
    {
        var organization = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId);
        if (organization == null)
            return new List<EmployeePayroll>();

        var employeeQuery = _context.Employees
            .Include(e => e.Unit)
            .ThenInclude(u => u.UnitType)
            .Where(e => e.OrganizationId == organizationId && e.IsActive);
        if (unitId.HasValue)
            employeeQuery = employeeQuery.Where(e => e.UnitId == unitId.Value);
        if (employeeIds != null && employeeIds.Any())
            employeeQuery = employeeQuery.Where(e => employeeIds.Contains(e.Id));

        var employees = await employeeQuery.ToListAsync();
        if (!employees.Any())
            return new List<EmployeePayroll>();

        var units = await _context.Units
            .Include(u => u.UnitType)
            .Where(u => u.OrganizationId == organizationId && u.IsActive)
            .ToListAsync();

        var unitCoefficientMap = await GetUnitCoefficientMapAsync(organizationId);
        ApplyUnitCoefficients(units, unitCoefficientMap);
        if (units.Any())
        {
            var unitLookup = units.ToDictionary(u => u.Id, u => u);
            foreach (var employee in employees)
            {
                if (employee.UnitId.HasValue && unitLookup.TryGetValue(employee.UnitId.Value, out var unit))
                {
                    employee.Unit = unit;
                }
            }
        }

        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organizationId)
            .Where(h => h.Date.Year == year && h.Date.Month == month)
            .ToListAsync();

        var leaves = await _context.Leaves
            .Include(l => l.LeaveType)
            .Where(l => l.Employee.OrganizationId == organizationId)
            .Where(l => l.Date.Year == year && l.Date.Month == month)
            .ToListAsync();

        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organizationId)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .ToListAsync();

        var prevMonth = new DateOnly(year, month, 1).AddDays(-1);
        var prevMonthShifts = await _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organizationId)
            .Where(s => s.Date == prevMonth && s.SpansNextDay)
            .ToListAsync();

        var payrollOptions = GetPayrollOptions();
        return CalculateEmployeePayrolls(
            employees,
            shifts,
            prevMonthShifts,
            holidays,
            leaves,
            organization,
            year,
            month,
            organization.NightStartTime,
            organization.NightEndTime,
            payrollOptions,
            units);
    }

    private async Task SaveBordroResultsAsync(int organizationId, int year, int month, List<Bordro4AResult> results4A, List<Bordro4BResult> results4B, List<int> employeeIds)
    {
        var existing4A = await _context.BordroResults4A
            .Where(r => r.OrganizationId == organizationId && r.Year == year && r.Month == month && employeeIds.Contains(r.EmployeeId))
            .ToListAsync();
        if (existing4A.Any())
            _context.BordroResults4A.RemoveRange(existing4A);

        var existing4B = await _context.BordroResults4B
            .Where(r => r.OrganizationId == organizationId && r.Year == year && r.Month == month && employeeIds.Contains(r.EmployeeId))
            .ToListAsync();
        if (existing4B.Any())
            _context.BordroResults4B.RemoveRange(existing4B);

        foreach (var item in results4A)
        {
            _context.BordroResults4A.Add(new BordroResult4A
            {
                OrganizationId = organizationId,
                EmployeeId = item.EmployeeId,
                Year = item.Year,
                Month = item.Month,
                NobetPuani = item.NobetPuani,
                SaatUcreti = item.SaatUcreti,
                YogunBakimVar = item.YogunBakimVar,
                NormalServisNobetSaati = item.NormalServisNobetSaati,
                YogunBakimNobetSaati = item.YogunBakimNobetSaati,
                NormalServisBayramSaati = item.NormalServisBayramSaati,
                YogunBakimBayramSaati = item.YogunBakimBayramSaati,
                BayramFarkiNobetSaati = item.BayramFarkiNobetSaati,
                NormalServisNobetToplamTutar = item.NormalServisNobetToplamTutar,
                YogunBakimNobetToplamTutar = item.YogunBakimNobetToplamTutar,
                NormalServisBayramToplamTutar = item.NormalServisBayramToplamTutar,
                YogunBakimBayramToplamTutar = item.YogunBakimBayramToplamTutar,
                BayramFarkiToplamTutar = item.BayramFarkiToplamTutar,
                GenelToplamTutar = item.GenelToplamTutar,
                DamgaVergisi = item.DamgaVergisi,
                EleGecenToplam = item.EleGecenToplam
            });
        }

        foreach (var item in results4B)
        {
            _context.BordroResults4B.Add(new BordroResult4B
            {
                OrganizationId = organizationId,
                EmployeeId = item.EmployeeId,
                Year = item.Year,
                Month = item.Month,
                NobetPuani = item.NobetPuani,
                SaatUcreti = item.SaatUcreti,
                YogunBakimVar = item.YogunBakimVar,
                NormalServisNobetSaati = item.NormalServisNobetSaati,
                YogunBakimNobetSaati = item.YogunBakimNobetSaati,
                NormalServisBayramSaati = item.NormalServisBayramSaati,
                YogunBakimBayramSaati = item.YogunBakimBayramSaati,
                BayramFarkiNobetSaati = item.BayramFarkiNobetSaati,
                NormalServisNobetToplamTutar = item.NormalServisNobetToplamTutar,
                YogunBakimNobetToplamTutar = item.YogunBakimNobetToplamTutar,
                NormalServisBayramToplamTutar = item.NormalServisBayramToplamTutar,
                YogunBakimBayramToplamTutar = item.YogunBakimBayramToplamTutar,
                BayramFarkiToplamTutar = item.BayramFarkiToplamTutar,
                GenelToplamTutarPek = item.GenelToplamTutarPek,
                MaluliyetYaslilikEmeklilikDev = item.MaluliyetYaslilikEmeklilikDev,
                GssDev = item.GssDev,
                KisaVadSigKolPrim = item.KisaVadSigKolPrim,
                GelirToplami = item.GelirToplami,
                DamgaVergisi = item.DamgaVergisi,
                MaluliyetYaslilikEmeklilikDevKesinti = item.MaluliyetYaslilikEmeklilikDevKesinti,
                GssDevKesinti = item.GssDevKesinti,
                KisaVadSigKolPrimKesinti = item.KisaVadSigKolPrimKesinti,
                MaluliyetYaslilikEmeklilikKisi = item.MaluliyetYaslilikEmeklilikKisi,
                GssKisi = item.GssKisi,
                KesintiToplami = item.KesintiToplami,
                EleGecenToplam = item.EleGecenToplam
            });
        }

        await _context.SaveChangesAsync();
    }

    private static Bordro4AResult Aggregate4A(List<BordroResult4A> items, Employee employee)
    {
        return new Bordro4AResult
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            EmployeeTitle = employee.Title,
            UnitId = employee.UnitId,
            UnitName = employee.Unit?.Name,
            CalisilanBirimler = employee.UnitId?.ToString(),
            Year = items.First().Year,
            Month = items.First().Month,
            NobetPuani = items.Max(i => i.NobetPuani),
            YogunBakimVar = items.Any(i => i.YogunBakimVar),
            SaatUcreti = items.Max(i => i.SaatUcreti),
            NormalServisNobetSaati = items.Sum(i => i.NormalServisNobetSaati),
            YogunBakimNobetSaati = items.Sum(i => i.YogunBakimNobetSaati),
            NormalServisBayramSaati = items.Sum(i => i.NormalServisBayramSaati),
            YogunBakimBayramSaati = items.Sum(i => i.YogunBakimBayramSaati),
            BayramFarkiNobetSaati = items.Sum(i => i.BayramFarkiNobetSaati),
            NormalServisNobetToplamTutar = items.Sum(i => i.NormalServisNobetToplamTutar),
            YogunBakimNobetToplamTutar = items.Sum(i => i.YogunBakimNobetToplamTutar),
            NormalServisBayramToplamTutar = items.Sum(i => i.NormalServisBayramToplamTutar),
            YogunBakimBayramToplamTutar = items.Sum(i => i.YogunBakimBayramToplamTutar),
            BayramFarkiToplamTutar = items.Sum(i => i.BayramFarkiToplamTutar),
            GenelToplamTutar = items.Sum(i => i.GenelToplamTutar),
            DamgaVergisi = items.Sum(i => i.DamgaVergisi),
            EleGecenToplam = items.Sum(i => i.EleGecenToplam)
        };
    }

    private static Bordro4BResult Aggregate4B(List<BordroResult4B> items, Employee employee)
    {
        return new Bordro4BResult
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            EmployeeTitle = employee.Title,
            UnitId = employee.UnitId,
            UnitName = employee.Unit?.Name,
            CalisilanBirimler = employee.UnitId?.ToString(),
            Year = items.First().Year,
            Month = items.First().Month,
            NobetPuani = items.Max(i => i.NobetPuani),
            YogunBakimVar = items.Any(i => i.YogunBakimVar),
            SaatUcreti = items.Max(i => i.SaatUcreti),
            NormalServisNobetSaati = items.Sum(i => i.NormalServisNobetSaati),
            YogunBakimNobetSaati = items.Sum(i => i.YogunBakimNobetSaati),
            NormalServisBayramSaati = items.Sum(i => i.NormalServisBayramSaati),
            YogunBakimBayramSaati = items.Sum(i => i.YogunBakimBayramSaati),
            BayramFarkiNobetSaati = items.Sum(i => i.BayramFarkiNobetSaati),
            NormalServisNobetToplamTutar = items.Sum(i => i.NormalServisNobetToplamTutar),
            YogunBakimNobetToplamTutar = items.Sum(i => i.YogunBakimNobetToplamTutar),
            NormalServisBayramToplamTutar = items.Sum(i => i.NormalServisBayramToplamTutar),
            YogunBakimBayramToplamTutar = items.Sum(i => i.YogunBakimBayramToplamTutar),
            BayramFarkiToplamTutar = items.Sum(i => i.BayramFarkiToplamTutar),
            GenelToplamTutarPek = items.Sum(i => i.GenelToplamTutarPek),
            MaluliyetYaslilikEmeklilikDev = items.Sum(i => i.MaluliyetYaslilikEmeklilikDev),
            GssDev = items.Sum(i => i.GssDev),
            KisaVadSigKolPrim = items.Sum(i => i.KisaVadSigKolPrim),
            GelirToplami = items.Sum(i => i.GelirToplami),
            DamgaVergisi = items.Sum(i => i.DamgaVergisi),
            MaluliyetYaslilikEmeklilikDevKesinti = items.Sum(i => i.MaluliyetYaslilikEmeklilikDevKesinti),
            GssDevKesinti = items.Sum(i => i.GssDevKesinti),
            KisaVadSigKolPrimKesinti = items.Sum(i => i.KisaVadSigKolPrimKesinti),
            MaluliyetYaslilikEmeklilikKisi = items.Sum(i => i.MaluliyetYaslilikEmeklilikKisi),
            GssKisi = items.Sum(i => i.GssKisi),
            KesintiToplami = items.Sum(i => i.KesintiToplami),
            EleGecenToplam = items.Sum(i => i.EleGecenToplam)
        };
    }

    private async Task<List<Bordro4AResult>> Project4A(List<BordroResult4A> items)
    {
        var employeeLookup = await _context.Employees
            .Include(e => e.Unit)
            .ToDictionaryAsync(e => e.Id, e => e);
        return items.GroupBy(i => i.EmployeeId)
            .Select(g => Aggregate4A(g.ToList(), employeeLookup[g.Key]))
            .ToList();
    }

    private async Task<List<Bordro4BResult>> Project4B(List<BordroResult4B> items)
    {
        var employeeLookup = await _context.Employees
            .Include(e => e.Unit)
            .ToDictionaryAsync(e => e.Id, e => e);
        return items.GroupBy(i => i.EmployeeId)
            .Select(g => Aggregate4B(g.ToList(), employeeLookup[g.Key]))
            .ToList();
    }

    private PayrollOptions GetPayrollOptions()
    {
        var options = new PayrollOptions();
        _configuration.GetSection("Payroll").Bind(options);
        return options;
    }

    private async Task<BordroOptions> GetBordroOptionsAsync(int organizationId, string? cadreType = null)
    {
        await EnsureBordroSabitleriAsync(organizationId);

        var options = new BordroOptions();
        _configuration.GetSection("Bordro").Bind(options);

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var sabitler = await _context.BordroSabitleri
            .Where(s => s.OrganizationId == organizationId && s.IsActive)
            .Where(s => s.ValidFrom <= now && (s.ValidTo == null || s.ValidTo >= now))
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(cadreType))
        {
            var cadreList = sabitler.Where(s => string.Equals(s.CadreType, cadreType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (cadreList.Any())
                sabitler = cadreList;
        }

        var map = sabitler
            .GroupBy(s => s.Key)
            .ToDictionary(g => g.Key, g => g.First().Value);

        if (map.TryGetValue("MEMUR_MAAS_KATSAYISI", out var memur)) options.MemurMaasKatsayisi = memur;
        if (map.TryGetValue("YOGUN_BAKIM_CARPANI", out var yb)) options.YogunBakimCarpani = yb;
        if (map.TryGetValue("BAYRAM_NOBETI_CARPANI", out var nb)) options.NormalBayramCarpan = nb;
        if (map.TryGetValue("BAYRAM_NOBETI_YOGUN_BAKIM_CARPANI", out var ybBay)) options.YogunBayramCarpan = ybBay;
        if (map.TryGetValue("BAYRAM_FARKI_CARPANI", out var fark)) options.BayramFarkiCarpan = fark;
        if (map.TryGetValue("DAMGA_VERGISI_ORANI", out var damga)) options.DamgaVergisiOrani = damga;
        if (map.TryGetValue("MALULIYET_YASLILIK_EMEKLILIK_DEV_ORANI", out var myeDev)) options.SgkMaluliyetDevOrani = myeDev;
        if (map.TryGetValue("GSS_DEV_ORANI", out var gssDev)) options.SgkGssDevOrani = gssDev;
        if (map.TryGetValue("KISA_VAD_SIG_KOL_PRIM_ORANI", out var kisaVad)) options.SgkIsKazasiOrani = kisaVad;
        if (map.TryGetValue("MALULIYET_YASLILIK_EMEKLILIK_KISI_ORANI", out var myeKisi)) options.SgkMaluliyetKisiOrani = myeKisi;
        if (map.TryGetValue("GSS_KISI_ORANI", out var gssKisi)) options.SgkGssKisiOrani = gssKisi;

        if (options.DamgaVergisiOrani > 0)
            options.DamgaVergisiOrani = Math.Round(options.DamgaVergisiOrani, 5, MidpointRounding.AwayFromZero);

        return options;
    }

    private async Task<Dictionary<int, decimal>> GetUnitCoefficientMapAsync(int organizationId)
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
        var sabitler = await _context.BordroSabitleri
            .Where(s => s.OrganizationId == organizationId && s.IsActive)
            .Where(s => s.ValidFrom <= today && (s.ValidTo == null || s.ValidTo >= today))
            .ToListAsync();

        sabitler = sabitler
            .Where(s => IsUnitCoefficientKey(s.Key))
            .ToList();

        var map = new Dictionary<int, decimal>();
        foreach (var sabit in sabitler)
        {
            var unitIds = ParseUnitIds(sabit.WorkingUnitIds);
            if (unitIds.Count == 0)
            {
                map[0] = sabit.Value;
                continue;
            }

            foreach (var unitId in unitIds)
            {
                map[unitId] = sabit.Value;
            }
        }

        return map;
    }

    private static void ApplyUnitCoefficients(List<Unit> units, Dictionary<int, decimal> map)
    {
        foreach (var unit in units)
        {
            if (map.TryGetValue(unit.Id, out var coefficient))
            {
                unit.Coefficient = coefficient;
            }
            else if (map.TryGetValue(0, out var defaultCoefficient))
            {
                unit.Coefficient = defaultCoefficient;
            }
            else if (unit.UnitType?.DefaultCoefficient > 0)
            {
                unit.Coefficient = unit.UnitType.DefaultCoefficient;
            }
        }
    }

    private static List<int> ParseUnitIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<int>();

        return value
            .Replace(';', ',')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => int.TryParse(id, out var parsed) ? parsed : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    private static bool IsUnitCoefficientKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return key.Replace(" ", string.Empty)
            .Contains("BIRIM_KATSAYI", StringComparison.OrdinalIgnoreCase);
    }

    private List<BordroSabitleriTemplate> BuildDefaultBordroTemplates()
    {
        var options = new BordroOptions();
        _configuration.GetSection("Bordro").Bind(options);

        return new List<BordroSabitleriTemplate>
        {
            new() { Key = "MEMUR_MAAS_KATSAYISI", Value = options.MemurMaasKatsayisi, Description = "Memur maaş katsayısı", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "YOGUN_BAKIM_CARPANI", Value = options.YogunBakimCarpani, Description = "Yoğun bakım çarpanı", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "BAYRAM_NOBETI_CARPANI", Value = options.NormalBayramCarpan, Description = "Bayram nöbeti çarpanı", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "BAYRAM_NOBETI_YOGUN_BAKIM_CARPANI", Value = options.YogunBayramCarpan, Description = "Bayram nöbeti yoğun bakım çarpanı", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "BAYRAM_FARKI_CARPANI", Value = options.BayramFarkiCarpan, Description = "Bayram farkı çarpanı", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "DAMGA_VERGISI_ORANI", Value = options.DamgaVergisiOrani, Description = "Damga vergisi oranı", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "MALULIYET_YASLILIK_EMEKLILIK_DEV_ORANI", Value = options.SgkMaluliyetDevOrani, Description = "SGK maluliyet/yaşlılık/emeklilik (işveren)", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "GSS_DEV_ORANI", Value = options.SgkGssDevOrani, Description = "SGK GSS (işveren)", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "KISA_VAD_SIG_KOL_PRIM_ORANI", Value = options.SgkIsKazasiOrani, Description = "Kısa vadeli sigorta kolu primi", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "MALULIYET_YASLILIK_EMEKLILIK_KISI_ORANI", Value = options.SgkMaluliyetKisiOrani, Description = "SGK maluliyet/yaşlılık/emeklilik (kişi)", ValueType = "ORAN", CreatedBy = "system" },
            new() { Key = "GSS_KISI_ORANI", Value = options.SgkGssKisiOrani, Description = "SGK GSS (kişi)", ValueType = "ORAN", CreatedBy = "system" }
        };
    }

    private async Task<Dictionary<string, int>> GetPersonelPuanMapAsync(int organizationId)
    {
        return await _context.PersonelNobetPuan
            .Where(p => p.OrganizationId == organizationId && p.IsActive)
            .Where(p => !string.IsNullOrEmpty(p.TcKimlik))
            .ToDictionaryAsync(p => p.TcKimlik, p => p.YPuan);
    }

    private static string GetAyAdi(int ay)
    {
        var aylar = new[] { "", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık" };
        return ay >= 1 && ay <= 12 ? aylar[ay] : "";
    }

    private sealed record ShiftDaySegment(DateOnly Date, int StartMinutes, int EndMinutes, decimal GrossHours, bool IsFirstDay);

    private List<EmployeePayroll> CalculateEmployeePayrolls(
        List<Employee> employees,
        List<Shift> shifts,
        List<Shift> previousMonthShifts,
        List<Holiday> holidays,
        List<Leave> leaves,
        Organization organization,
        int year, int month,
        TimeOnly nightStart, TimeOnly nightEnd,
        PayrollOptions payrollOptions,
        List<Unit> units)
    {
        var payrolls = new List<EmployeePayroll>();
        var weekendDays = organization.WeekendDays.Split(',').Select(int.Parse).ToList();
        var unitLookup = units.ToDictionary(u => u.Id, u => u);

        foreach (var employee in employees)
        {
            var employeeShifts = shifts.Where(s => s.EmployeeId == employee.Id).ToList();
            var employeeLeaves = leaves.Where(l => l.EmployeeId == employee.Id).ToList();
            var prevMonthShift = previousMonthShifts.FirstOrDefault(s => s.EmployeeId == employee.Id);
            var unit = GetEmployeeUnit(employee, unitLookup);
            var isRiskGroup = GetDefaultRiskGroup(unit);
            var leaveCounts = CountLeaveDays(employeeLeaves);

            var payroll = new EmployeePayroll
            {
                Employee = employee,
                ShiftDetails = new List<ShiftDetail>(),
                LeaveDays = leaveCounts.total,
                AnnualLeaveDays = leaveCounts.annual,
                SickLeaveDays = leaveCounts.sick
            };

            payroll.RequiredHours = CalculateRequiredHours(employee, year, month, holidays, weekendDays, isRiskGroup, employeeLeaves);

            if (prevMonthShift != null && !prevMonthShift.IsDayOff && prevMonthShift.OvernightHoursMode == 0)
            {
                var spilledHours = CalculateHoursAfterMidnight(prevMonthShift, employee);
                var spilledNightHours = CalculateNightHoursAfterMidnight(prevMonthShift, nightStart, nightEnd);
                payroll.TotalWorkedHours += spilledHours;
                payroll.NightHours += spilledNightHours;
            }

            foreach (var shift in employeeShifts)
            {
                var holiday = holidays.FirstOrDefault(h => h.Date == shift.Date);
                var isWeekend = weekendDays.Contains((int)shift.Date.DayOfWeek);

                var detailIsIntensive = IsIntensiveCareByGroup(shift.WorkGroupTypeId, shift.IsRiskGroup, unit);
                var detail = new ShiftDetail
                {
                    Date = shift.Date,
                    StartTime = shift.StartTime,
                    EndTime = shift.EndTime,
                    SpansNextDay = shift.SpansNextDay,
                    IsDayOff = shift.IsDayOff,
                    IsWeekend = isWeekend,
                    IsHoliday = holiday != null,
                    HolidayName = holiday?.Name,
                    IsIntensiveCare = detailIsIntensive
                };

                if (shift.IsDayOff)
                {
                    payroll.DayOffCount++;
                }
                else
                {
                    payroll.WorkedDays++;
                    var hoursThisMonth = CalculateShiftHoursForPayroll(shift, employee, year, month);
                    detail.TotalHours = hoursThisMonth;
                    payroll.TotalWorkedHours += hoursThisMonth;

                    var nightHours = CalculateNightHours(shift, employee, nightStart, nightEnd, year, month);
                    detail.NightHours = nightHours;
                    payroll.NightHours += nightHours;

                    var ticketCount = CalculateTicketCount(shift.StartTime, shift.EndTime, employee.HasDoubleTicketRight);
                    payroll.TicketCount += ticketCount;
                    if (ticketCount > 0)
                        payroll.TransportationDays++;

                    var holidayHours = CalculateHolidayHoursForShift(shift, employee, holidays, year, month);
                    detail.HolidayHours = holidayHours;
                    payroll.HolidayHours += holidayHours;

                    var weekendHours = CalculateWeekendHoursForShift(shift, employee, weekendDays, holidays, year, month);
                    detail.WeekendHours = weekendHours;
                    payroll.WeekendHours += weekendHours;

                    foreach (var segment in GetShiftNetSegmentsForMonth(shift, employee, year, month))
                    {
                        payroll.CalculationSegments.Add(segment);
                    }
                }

                payroll.ShiftDetails.Add(detail);
            }

            foreach (var leave in employeeLeaves)
            {
                if (payroll.ShiftDetails.Any(d => d.Date == leave.Date))
                    continue;

                var holiday = holidays.FirstOrDefault(h => h.Date == leave.Date);
                var isWeekend = weekendDays.Contains((int)leave.Date.DayOfWeek);

                payroll.ShiftDetails.Add(new ShiftDetail
                {
                    Date = leave.Date,
                    IsLeave = true,
                    LeaveCode = leave.LeaveType?.Code,
                    LeaveColor = leave.LeaveType?.Color,
                    IsWeekend = isWeekend,
                    IsHoliday = holiday != null,
                    HolidayName = holiday?.Name,
                    Note = leave.Notes
                });
            }

            payroll.ShiftDetails = payroll.ShiftDetails.OrderBy(d => d.Date).ToList();

            var workedDayCount = payroll.CalculationSegments
                .Where(s => s.Hours > 0)
                .Select(s => s.Date)
                .Distinct()
                .Count();
            if (workedDayCount > 0)
                payroll.WorkedDays = workedDayCount;

            payroll.IsIntensiveCare = payroll.ShiftDetails.Any(d => d.IsIntensiveCare);

            FinalizePayrollTotals(payroll, employee, organization, holidays, weekendDays, isRiskGroup, employeeLeaves, payrollOptions);

            payrolls.Add(payroll);
        }

        return payrolls;
    }

    private decimal CalculateRequiredHours(Employee employee, int year, int month, List<Holiday> holidays, List<int> weekendDays, bool isRiskGroup, List<Leave>? leaves = null)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        decimal total = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            total += CalculateRequiredHoursForDate(employee, date, holidays, weekendDays, isRiskGroup, leaves);
        }

        return Math.Round(total, 2);
    }

    private decimal CalculateRequiredHoursForDate(Employee employee, DateOnly date, List<Holiday> holidays, List<int> weekendDays, bool isRiskGroup, List<Leave>? leaves = null)
    {
        var isWeekend = weekendDays.Contains((int)date.DayOfWeek);
        var holiday = holidays.FirstOrDefault(h => h.Date == date);
        var leaveExists = leaves?.Any(l => l.Date == date) == true;

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

    private (decimal total, decimal normal, decimal intensive) CalculateDailyOvertimeHours(
        decimal workedHours, decimal requiredHours, bool isIntensive, PayrollOptions options)
    {
        if (workedHours <= requiredHours)
            return (0, 0, 0);

        var overtime = Math.Round(workedHours - requiredHours, 2);
        if (isIntensive)
            return (overtime, 0, overtime);

        return (overtime, overtime, 0);
    }

    private void FinalizePayrollTotals(EmployeePayroll payroll, Employee employee, Organization organization, List<Holiday> holidays, List<int> weekendDays, bool isRiskGroup, List<Leave> employeeLeaves, PayrollOptions options)
    {
        payroll.CalculationSegments = payroll.CalculationSegments
            .Where(s => s.Hours > 0)
            .ToList();

        var dayMap = payroll.CalculationSegments
            .GroupBy(s => s.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var normalOvertimeTotal = 0m;
        var intensiveOvertimeTotal = 0m;
        foreach (var kvp in dayMap)
        {
            var date = kvp.Key;
            var segments = kvp.Value;

            var isHoliday = holidays.Any(h => h.Date == date);
            var isWeekend = weekendDays.Contains((int)date.DayOfWeek);

            var totalHours = segments.Sum(s => s.Hours);
            var intensiveHours = segments.Where(s => s.IsIntensiveCare).Sum(s => s.Hours);
            var normalHours = totalHours - intensiveHours;

            var requiredHours = CalculateRequiredHoursForDate(employee, date, holidays, weekendDays, isRiskGroup, employeeLeaves);
            var overtime = Math.Max(0, totalHours - requiredHours);
            if (overtime > 0 && totalHours > 0)
            {
                var intensiveRatio = intensiveHours / totalHours;
                var intensiveOvertime = overtime * intensiveRatio;
                var normalOvertime = overtime - intensiveOvertime;
                normalOvertimeTotal += normalOvertime;
                intensiveOvertimeTotal += intensiveOvertime;
            }
            else if (overtime > 0)
            {
                normalOvertimeTotal += overtime;
            }

            if (isHoliday)
            {
                payroll.HolidayHours += totalHours;
                payroll.NormalHolidayHours += normalHours;
                payroll.IntensiveHolidayHours += intensiveHours;
            }

            if (isWeekend && !isHoliday)
                payroll.WeekendHours += totalHours;
        }

        payroll.TotalWorkedHours = RoundToHalfHour(payroll.TotalWorkedHours);
        payroll.NightHours = RoundToHalfHour(payroll.NightHours);
        payroll.WeekendHours = RoundToHalfHour(payroll.WeekendHours);
        payroll.HolidayHours = RoundToHalfHour(payroll.HolidayHours);
        payroll.NormalHolidayHours = RoundToHalfHour(payroll.NormalHolidayHours);
        payroll.IntensiveHolidayHours = RoundToHalfHour(payroll.IntensiveHolidayHours);

        var totalOvertime = normalOvertimeTotal + intensiveOvertimeTotal;
        var totalBayram = payroll.NormalHolidayHours + payroll.IntensiveHolidayHours;

        var bayramFarki = Math.Max(0, totalBayram - totalOvertime);
        var yeniFazlaMesai = Math.Max(0, totalOvertime - totalBayram);

        if (totalOvertime > 0)
        {
            var normalOran = normalOvertimeTotal / totalOvertime;
            var yogunOran = intensiveOvertimeTotal / totalOvertime;
            normalOvertimeTotal = yeniFazlaMesai * normalOran;
            intensiveOvertimeTotal = yeniFazlaMesai * yogunOran;
        }
        else
        {
            normalOvertimeTotal = 0;
            intensiveOvertimeTotal = 0;
        }

        var limited = LimitOvertimeHours(normalOvertimeTotal, intensiveOvertimeTotal, options.OvertimeLimitHours);
        payroll.NormalOvertimeHours = RoundToHalfHour(limited.normal);
        payroll.IntensiveOvertimeHours = RoundToHalfHour(limited.intensive);
        payroll.OvertimeHours = RoundToHalfHour(payroll.NormalOvertimeHours + payroll.IntensiveOvertimeHours);
        payroll.HolidayDifferenceHours = RoundToHalfHour(bayramFarki);
    }

    private static Unit? GetEmployeeUnit(Employee employee, Dictionary<int, Unit> unitLookup)
    {
        if (!employee.UnitId.HasValue)
            return null;

        return unitLookup.TryGetValue(employee.UnitId.Value, out var unit) ? unit : null;
    }

    private static bool IsIntensiveCareUnit(Unit? unit)
    {
        if (unit == null)
            return false;

        var unitTypeName = unit.UnitType?.Name ?? string.Empty;
        if (unitTypeName.Contains("yoğun", StringComparison.OrdinalIgnoreCase))
            return true;

        if (unitTypeName.Contains("radyasyon", StringComparison.OrdinalIgnoreCase))
            return true;

        return unit.Coefficient >= 1.5m;
    }

    private static int GetDefaultWorkGroupTypeId(Unit? unit)
    {
        if (unit?.UnitType?.Name?.Contains("yoğun", StringComparison.OrdinalIgnoreCase) == true)
            return (int)WorkGroupType.IntensiveCare;

        return (int)WorkGroupType.Normal;
    }

    private static bool GetDefaultRiskGroup(Unit? unit)
    {
        return unit?.UnitType?.Name?.Contains("radyasyon", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static int ResolveWorkGroupTypeId(int? requestedTypeId, ShiftTemplate? template, Unit? unit)
    {
        if (requestedTypeId.HasValue)
            return requestedTypeId.Value;

        if (template?.WorkGroupTypeId.HasValue == true)
            return template.WorkGroupTypeId.Value;

        return GetDefaultWorkGroupTypeId(unit);
    }

    private static bool ResolveRiskGroup(bool? requestedRisk, ShiftTemplate? template, Unit? unit)
    {
        if (requestedRisk.HasValue)
            return requestedRisk.Value;

        if (template != null)
            return template.IsRiskGroup;

        return GetDefaultRiskGroup(unit);
    }

    private static bool IsIntensiveCareByGroup(int? workGroupTypeId, bool isRiskGroup, Unit? unit)
    {
        if (workGroupTypeId.HasValue)
            return workGroupTypeId.Value == (int)WorkGroupType.IntensiveCare || isRiskGroup;

        return isRiskGroup || IsIntensiveCareUnit(unit);
    }

    private static decimal RoundToHalfHour(decimal hours)
    {
        return Math.Round(hours * 2, MidpointRounding.AwayFromZero) / 2m;
    }

    private static (int total, int annual, int sick) CountLeaveDays(List<Leave> leaves)
    {
        var total = leaves.Count;
        var annual = leaves.Count(l =>
            string.Equals(l.LeaveType?.Category, "annual", StringComparison.OrdinalIgnoreCase) ||
            (l.LeaveType?.Code?.StartsWith("Y", StringComparison.OrdinalIgnoreCase) == true));
        var sick = leaves.Count(l =>
            string.Equals(l.LeaveType?.Category, "health", StringComparison.OrdinalIgnoreCase) ||
            (l.LeaveType?.Code?.StartsWith("H", StringComparison.OrdinalIgnoreCase) == true));

        return (total, annual, sick);
    }

    private static int CalculateTicketCount(TimeOnly start, TimeOnly end, bool hasDoubleTicketRight)
    {
        int ticketCount = 0;

        if (start != new TimeOnly(8, 0))
            ticketCount++;

        if (end != new TimeOnly(17, 0))
            ticketCount++;

        if (hasDoubleTicketRight)
            ticketCount *= 2;

        return ticketCount;
    }

    private decimal CalculateHolidayHoursForShift(Shift shift, Employee employee, List<Holiday> holidays, int year, int month)
    {
        var holiday = holidays.FirstOrDefault(h => h.Date == shift.Date);
        if (holiday == null || shift.IsDayOff)
            return 0;

        if (holiday.IsHalfDay && holiday.HalfDayWorkHours.HasValue)
        {
            var hours = CalculateShiftHoursForPayroll(shift, employee, year, month);
            return Math.Max(0, hours - holiday.HalfDayWorkHours.Value);
        }

        return CalculateShiftHoursForPayroll(shift, employee, year, month);
    }

    private decimal CalculateHolidayHoursForAttendance(TimeAttendance attendance, List<Holiday> holidays, int year, int month)
    {
        var holiday = holidays.FirstOrDefault(h => h.Date == attendance.Date);
        if (holiday == null || attendance.Type == AttendanceType.DayOff || attendance.WorkedHours is null)
            return 0;

        if (holiday.IsHalfDay && holiday.HalfDayWorkHours.HasValue)
        {
            return Math.Max(0, attendance.WorkedHours.Value - holiday.HalfDayWorkHours.Value);
        }

        return attendance.WorkedHours.Value;
    }

    private decimal CalculateWeekendHoursForShift(Shift shift, Employee employee, List<int> weekendDays, List<Holiday> holidays, int year, int month)
    {
        if (shift.IsDayOff)
            return 0;

        var isWeekend = weekendDays.Contains((int)shift.Date.DayOfWeek);
        var isHoliday = holidays.Any(h => h.Date == shift.Date);

        if (!isWeekend || isHoliday)
            return 0;

        return CalculateShiftHoursForPayroll(shift, employee, year, month);
    }

    private decimal CalculateWeekendHoursForAttendance(TimeAttendance attendance, List<int> weekendDays, List<Holiday> holidays, int year, int month)
    {
        if (attendance.Type == AttendanceType.DayOff || attendance.WorkedHours is null)
            return 0;

        var isWeekend = weekendDays.Contains((int)attendance.Date.DayOfWeek);
        var isHoliday = holidays.Any(h => h.Date == attendance.Date);

        if (!isWeekend || isHoliday)
            return 0;

        return attendance.WorkedHours.Value;
    }

    private static decimal CalculateHoursAfterThreshold(int startMinutes, int endMinutes, int thresholdMinutes)
    {
        if (endMinutes <= thresholdMinutes)
            return 0;

        var minutesAfter = Math.Max(0, endMinutes - Math.Max(startMinutes, thresholdMinutes));
        return minutesAfter / 60m;
    }

    private int GetEffectiveBreakMinutes(Shift shift, Employee employee)
    {
        if (shift.BreakMinutes > 0)
            return shift.BreakMinutes;
        if (shift.ShiftTemplate?.BreakMinutes.HasValue == true)
            return shift.ShiftTemplate.BreakMinutes.Value;
        return 0;
    }

    private static decimal GetShiftGrossMinutes(Shift shift)
    {
        var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;

        if (shift.SpansNextDay)
            return (24 * 60 - startMinutes) + endMinutes;

        return Math.Max(0, endMinutes - startMinutes);
    }

    private static decimal CalculateNetShiftHours(Shift shift, int breakMinutes)
    {
        var grossMinutes = GetShiftGrossMinutes(shift);
        var netMinutes = Math.Max(0, grossMinutes - breakMinutes);
        return netMinutes / 60m;
    }

    private List<ShiftDaySegment> GetShiftDaySegments(Shift shift)
    {
        var segments = new List<ShiftDaySegment>();
        if (!shift.SpansNextDay)
        {
            var grossHours = GetShiftGrossMinutes(shift) / 60m;
            var startMinutesLocal = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
            var endMinutesLocal = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
            segments.Add(new ShiftDaySegment(shift.Date, startMinutesLocal, endMinutesLocal, grossHours, true));
            return segments;
        }

        var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
        var dayOneMinutes = 1440 - startMinutes;
        var dayTwoMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;

        segments.Add(new ShiftDaySegment(
            shift.Date,
            shift.StartTime.Hour * 60 + shift.StartTime.Minute,
            1440,
            Math.Max(0, dayOneMinutes / 60m),
            true));

        segments.Add(new ShiftDaySegment(
            shift.Date.AddDays(1),
            0,
            shift.EndTime.Hour * 60 + shift.EndTime.Minute,
            Math.Max(0, dayTwoMinutes / 60m),
            false));

        return segments;
    }

    private List<WorkSegment> GetShiftNetSegmentsForMonth(Shift shift, Employee employee, int year, int month)
    {
        var results = new List<WorkSegment>();
        if (shift.IsDayOff)
            return results;

        var breakMinutes = GetEffectiveBreakMinutes(shift, employee);
        var segments = GetShiftDaySegments(shift);

        if (!(shift.SpansNextDay && shift.OvernightHoursMode == 1))
        {
            segments = segments.Where(s => s.Date.Year == year && s.Date.Month == month).ToList();
        }

        var totalGross = segments.Sum(s => s.GrossHours);
        var netHours = CalculateNetShiftHours(shift, breakMinutes);
        var ratio = totalGross > 0 ? netHours / totalGross : 0;

        foreach (var segment in segments)
        {
            if (segment.GrossHours <= 0)
                continue;

            results.Add(new WorkSegment
            {
                Date = segment.Date,
                Hours = segment.GrossHours * ratio,
                IsIntensiveCare = IsIntensiveCareByGroup(shift.WorkGroupTypeId, shift.IsRiskGroup, employee.Unit)
            });
        }

        return results;
    }

    private List<WorkSegment> GetAttendanceSegments(TimeAttendance attendance)
    {
        var segments = new List<WorkSegment>();
        if (!attendance.CheckInTime.HasValue || !attendance.CheckOutTime.HasValue)
            return segments;

        var start = attendance.CheckInTime.Value;
        var end = attendance.CheckOutTime.Value;
        var spansNext = attendance.CheckOutToNextDay;

        var startMinutes = start.Hour * 60 + start.Minute;
        var endMinutes = end.Hour * 60 + end.Minute;

        if (!spansNext && endMinutes < startMinutes)
            spansNext = true;

        if (!spansNext)
        {
            var grossHours = Math.Max(0, (endMinutes - startMinutes) / 60m);
            segments.Add(new WorkSegment { Date = attendance.Date, Hours = grossHours, IsIntensiveCare = attendance.IsRiskGroup });
            return segments;
        }

        var dayOneMinutes = 1440 - startMinutes;
        var dayTwoMinutes = endMinutes;
        segments.Add(new WorkSegment { Date = attendance.Date, Hours = Math.Max(0, dayOneMinutes / 60m), IsIntensiveCare = attendance.IsRiskGroup });
        segments.Add(new WorkSegment { Date = attendance.Date.AddDays(1), Hours = Math.Max(0, dayTwoMinutes / 60m), IsIntensiveCare = attendance.IsRiskGroup });
        return segments;
    }

    private static (decimal normal, decimal intensive) LimitOvertimeHours(decimal normal, decimal intensive, decimal maxHours)
    {
        return FazlaMesaiLimitHelper.LimitFazlaMesaiSaati(normal, intensive, maxHours);
    }

    private decimal CalculateNightHours(Shift shift, Employee employee, TimeOnly nightStart, TimeOnly nightEnd, int year, int month)
    {
        var segments = GetShiftDaySegments(shift);
        var total = 0m;
        foreach (var segment in segments)
        {
            if (segment.Date.Year != year || segment.Date.Month != month)
                continue;

            total += CalculateFullNightHours(
                TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(segment.StartMinutes)),
                TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(segment.EndMinutes == 1440 ? 0 : segment.EndMinutes)),
                segment.EndMinutes == 1440,
                nightStart,
                nightEnd,
                0);
        }
        return RoundToHalfHour(total);
    }

    private static decimal CalculateFullNightHours(TimeOnly start, TimeOnly end, bool spansNextDay, TimeOnly nightStart, TimeOnly nightEnd, int breakMinutes)
    {
        var minutes = GetNightMinutesInRange(start, end, spansNextDay, nightStart, nightEnd);
        var netMinutes = Math.Max(0, minutes - breakMinutes);
        return netMinutes / 60m;
    }

    private static int GetNightMinutesInRange(TimeOnly start, TimeOnly end, bool spansNextDay, TimeOnly nightStart, TimeOnly nightEnd)
    {
        var startMinutes = start.Hour * 60 + start.Minute;
        var endMinutes = end.Hour * 60 + end.Minute;
        var nightStartMinutes = nightStart.Hour * 60 + nightStart.Minute;
        var nightEndMinutes = nightEnd.Hour * 60 + nightEnd.Minute;

        if (!spansNextDay && endMinutes < startMinutes)
            spansNextDay = true;

        if (!spansNextDay)
            return (int)CalculateHoursAfterThreshold(startMinutes, endMinutes, nightStartMinutes) * 60;

        var minutesBeforeMidnight = CalculateNightHoursBeforeMidnight(startMinutes, nightStartMinutes) * 60;
        var minutesAfterMidnight = CalculateNightHoursAfterMidnight(endMinutes, nightEndMinutes) * 60;
        return (int)(minutesBeforeMidnight + minutesAfterMidnight);
    }

    private static decimal CalculateNightHoursBeforeMidnight(int startMinutes, int nightStartMinutes)
    {
        if (startMinutes > nightStartMinutes)
            return (1440 - startMinutes) / 60m;
        return (1440 - nightStartMinutes) / 60m;
    }

    private static decimal CalculateNightHoursAfterMidnight(int endMinutes, int nightEndMinutes)
    {
        if (endMinutes <= nightEndMinutes)
            return endMinutes / 60m;
        return nightEndMinutes / 60m;
    }

    private static decimal CalculateNightHoursAfterMidnight(Shift shift, TimeOnly nightStart, TimeOnly nightEnd)
    {
        if (!shift.SpansNextDay)
            return 0;

        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
        var nightEndMinutes = nightEnd.Hour * 60 + nightEnd.Minute;
        return CalculateNightHoursAfterMidnight(endMinutes, nightEndMinutes);
    }

    private static decimal CalculateHoursAfterMidnight(Shift shift, Employee employee)
    {
        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
        var totalShiftMinutes = (int)(shift.TotalHours * 60) + shift.BreakMinutes;
        var minutesUntilMidnight = (24 * 60) - (shift.StartTime.Hour * 60 + shift.StartTime.Minute);
        var minutesAfterMidnight = totalShiftMinutes - minutesUntilMidnight;

        if (minutesAfterMidnight <= 0)
            return 0;

        var breakProportion = totalShiftMinutes > 0
            ? (decimal)minutesAfterMidnight / totalShiftMinutes
            : 0;
        var breakAfterMidnight = (int)(shift.BreakMinutes * breakProportion);
        var hoursAfterMidnight = (endMinutes - breakAfterMidnight) / 60m;
        return Math.Max(0, hoursAfterMidnight);
    }

    private decimal CalculateShiftHoursForPayroll(Shift shift, Employee employee, int year, int month)
    {
        var segments = GetShiftNetSegmentsForMonth(shift, employee, year, month);
        return segments.Sum(s => s.Hours);
    }
}
