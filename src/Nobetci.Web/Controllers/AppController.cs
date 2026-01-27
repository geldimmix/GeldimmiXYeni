using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Resources;
using Nobetci.Web.Services;

namespace Nobetci.Web.Controllers;

/// <summary>
/// Main application controller - handles shift scheduling
/// </summary>
public class AppController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppController> _logger;
    private readonly ISystemSettingsService _settingsService;
    private readonly IActivityLogService _activityLog;

    public AppController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer,
        IConfiguration configuration,
        ILogger<AppController> logger,
        ISystemSettingsService settingsService,
        IActivityLogService activityLog)
    {
        _context = context;
        _userManager = userManager;
        _localizer = localizer;
        _configuration = configuration;
        _logger = logger;
        _settingsService = settingsService;
        _activityLog = activityLog;
    }

    /// <summary>
    /// Main application page
    /// </summary>
    public async Task<IActionResult> Index(int? year, int? month, int? unitId)
    {
        var selectedYear = year ?? DateTime.Now.Year;
        var selectedMonth = month ?? DateTime.Now.Month;
        
        var organization = await GetOrCreateOrganizationAsync();
        
        // Check if premium and initialize units
        var isPremium = await IsPremiumUserAsync();
        int? selectedUnitId = null;
        Unit? defaultUnit = null;
        var units = new List<Unit>();
        var unitTypes = new List<UnitType>();
        
        string? unitLoadError = null;
        if (isPremium)
        {
            try
            {
                // Initialize defaults if needed
                await InitializeDefaultUnitTypesAsync(organization.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize default unit types");
            }
            
            try
            {
                await InitializeDefaultUnitAsync(organization.Id);
            }
            catch (Exception ex)
            {
                var innerEx = ex;
                while (innerEx.InnerException != null)
                    innerEx = innerEx.InnerException;
                _logger.LogWarning(ex, "Failed to initialize default unit: {Error}", innerEx.Message);
                unitLoadError = "Varsayılan birim oluşturulamadı: " + innerEx.Message;
            }
            
            try
            {
                // Load unit types
                unitTypes = await _context.UnitTypes
                    .Where(ut => ut.OrganizationId == organization.Id && ut.IsActive)
                    .OrderBy(ut => ut.SortOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load unit types");
            }
            
            try
            {
                // Load units with employees
                units = await _context.Units
                    .Include(u => u.UnitType)
                    .Include(u => u.Employees.Where(e => e.IsActive))
                    .Where(u => u.OrganizationId == organization.Id && u.IsActive)
                    .OrderBy(u => u.SortOrder)
                    .ToListAsync();
                
                // Get default unit if no unit selected
                defaultUnit = units.FirstOrDefault(u => u.IsDefault);
                selectedUnitId = unitId ?? defaultUnit?.Id;
            }
            catch (Exception ex)
            {
                var innerEx = ex;
                while (innerEx.InnerException != null)
                    innerEx = innerEx.InnerException;
                _logger.LogWarning(ex, "Failed to load units: {Error}", innerEx.Message);
                unitLoadError = "Birimler yüklenemedi: " + innerEx.Message;
            }
        }
        
        ViewBag.UnitLoadError = unitLoadError;
        
        // Get all employees (for employee modal - unfiltered pool)
        var allEmployees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();
        
        // Filter employees by unit for premium users (for shift calendar)
        List<Employee> employees;
        if (isPremium && selectedUnitId.HasValue)
        {
            employees = allEmployees.Where(e => e.UnitId == selectedUnitId).ToList();
        }
        else
        {
            employees = allEmployees;
        }
            
        var shiftTemplates = await _context.ShiftTemplates
            .Where(t => t.OrganizationId == organization.Id)
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();
            
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id)
            .Where(h => h.Date.Year == selectedYear && h.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get shifts for current month (filtered by unit for premium users)
        var shiftsQuery = _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == selectedYear && s.Date.Month == selectedMonth);
            
        if (isPremium && selectedUnitId.HasValue)
        {
            shiftsQuery = shiftsQuery.Where(s => s.Employee.UnitId == selectedUnitId);
        }
        
        var shifts = await shiftsQuery.ToListAsync();
        
        // Get overnight shifts from previous month's last day that span into current month
        var previousMonthLastDay = new DateOnly(selectedYear, selectedMonth, 1).AddDays(-1);
        var previousMonthQuery = _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date == previousMonthLastDay && s.SpansNextDay);
            
        if (isPremium && selectedUnitId.HasValue)
        {
            previousMonthQuery = previousMonthQuery.Where(s => s.Employee.UnitId == selectedUnitId);
        }
        
        var previousMonthShifts = await previousMonthQuery.ToListAsync();
            
        // Get leave types (system-wide + organization-specific)
        var leaveTypes = await _context.LeaveTypes
            .Where(lt => lt.OrganizationId == null || lt.OrganizationId == organization.Id)
            .Where(lt => lt.IsActive)
            .OrderBy(lt => lt.SortOrder)
            .ToListAsync();
            
        // Get leaves for current month (filtered by unit for premium users)
        var leavesQuery = _context.Leaves
            .Include(l => l.LeaveType)
            .Include(l => l.Employee)
            .Where(l => l.Employee.OrganizationId == organization.Id)
            .Where(l => l.Date.Year == selectedYear && l.Date.Month == selectedMonth);
            
        if (isPremium && selectedUnitId.HasValue)
        {
            leavesQuery = leavesQuery.Where(l => l.Employee.UnitId == selectedUnitId);
        }
        
        var leaves = await leavesQuery.ToListAsync();

        var employeeLimit = await GetEmployeeLimitAsync();
        var (canAccessAttendance, canAccessPayroll) = await GetFeatureAccessAsync();
        var isRegistered = User.Identity?.IsAuthenticated == true;
        
        // Get unit limit for registered users (from user settings or system default)
        var defaultUnitLimit = await _settingsService.GetDefaultUnitLimitAsync();
        var unitLimit = defaultUnitLimit;
        if (isRegistered)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                unitLimit = currentUser.UnitLimit > 0 ? currentUser.UnitLimit : defaultUnitLimit;
            }
        }
        
        var viewModel = new AppViewModel
        {
            Organization = organization,
            Employees = employees,
            AllEmployees = allEmployees,
            ShiftTemplates = shiftTemplates,
            Holidays = holidays,
            Shifts = shifts,
            PreviousMonthOvernightShifts = previousMonthShifts,
            LeaveTypes = leaveTypes,
            Leaves = leaves,
            Units = units,
            UnitTypes = unitTypes,
            SelectedUnitId = selectedUnitId,
            SelectedYear = selectedYear,
            SelectedMonth = selectedMonth,
            EmployeeLimit = employeeLimit,
            UnitLimit = unitLimit,
            TotalEmployeeCount = allEmployees.Count,
            IsRegistered = isRegistered,
            IsPremium = isPremium,
            // Feature access based on registration and admin settings
            CanUseSmartScheduling = isRegistered,
            CanUseTimesheet = isRegistered,
            CanExportExcel = isRegistered,
            CanAccessAttendance = canAccessAttendance,
            CanAccessPayroll = canAccessPayroll,
            CanManageUnits = isPremium,
            CanAccessCleaning = true // Always accessible, limits apply inside
        };

        return View(viewModel);
    }

    #region Employee API

    [HttpGet]
    [Route("api/employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var organization = await GetOrCreateOrganizationAsync();
        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new {
                e.Id,
                e.FullName,
                e.Title,
                e.IdentityNo,
                e.Color,
                e.DailyWorkHours,
                e.WeekendWorkMode,
                e.SaturdayWorkHours,
                e.UnitId
            })
            .ToListAsync();
            
        return Json(employees);
    }

    [HttpPost]
    [Route("api/employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] EmployeeDto dto)
    {
        var organization = await GetOrCreateOrganizationAsync();
        
        // Check employee limit
        var currentCount = await _context.Employees
            .CountAsync(e => e.OrganizationId == organization.Id && e.IsActive);
        var limit = await GetEmployeeLimitAsync();
        
        if (currentCount >= limit)
        {
            return BadRequest(new { error = _localizer["EmployeeLimitReached"].Value });
        }
        
        var employee = new Employee
        {
            OrganizationId = organization.Id,
            FullName = dto.FullName,
            Title = dto.Title,
            IdentityNo = dto.IdentityNo,
            Email = dto.Email,
            Phone = dto.Phone,
            Color = dto.Color ?? GetRandomColor(),
            DailyWorkHours = dto.DailyWorkHours > 0 ? dto.DailyWorkHours : 8,
            WeekendWorkMode = dto.WeekendWorkMode,
            SaturdayWorkHours = dto.SaturdayWorkHours,
            PositionType = dto.PositionType,
            AcademicTitle = dto.PositionType == "Academic" ? dto.AcademicTitle : null,
            ShiftScore = dto.ShiftScore > 0 ? dto.ShiftScore : 100,
            IsNonHealthServices = dto.IsNonHealthServices
        };
        
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.EmployeeCreated, 
            $"Personel eklendi: {employee.FullName}", 
            "Employee", employee.Id,
            new { employee.FullName, employee.Title, employee.IdentityNo });
        
        return Json(new { 
            id = employee.Id, 
            fullName = employee.FullName,
            title = employee.Title,
            identityNo = employee.IdentityNo,
            email = employee.Email,
            phone = employee.Phone,
            color = employee.Color,
            dailyWorkHours = employee.DailyWorkHours,
            weekendWorkMode = employee.WeekendWorkMode,
            saturdayWorkHours = employee.SaturdayWorkHours
        });
    }

    [HttpGet]
    [Route("api/employees/{id}")]
    public async Task<IActionResult> GetEmployee(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organization.Id && e.IsActive);
            
        if (employee == null)
            return NotFound();
            
        return Json(new {
            employee.Id,
            employee.FullName,
            employee.Title,
            employee.IdentityNo,
            employee.Email,
            employee.Phone,
            employee.Color,
            employee.DailyWorkHours,
            employee.WeekendWorkMode,
            employee.SaturdayWorkHours,
            employee.PositionType,
            employee.AcademicTitle,
            employee.ShiftScore,
            employee.IsNonHealthServices,
            employee.UnitId
        });
    }
    
    [HttpPut]
    [Route("api/employees/{id}")]
    public async Task<IActionResult> UpdateEmployee(int id, [FromBody] EmployeeDto dto)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organization.Id);
            
        if (employee == null)
            return NotFound();
            
        employee.FullName = dto.FullName;
        employee.Title = dto.Title;
        employee.IdentityNo = dto.IdentityNo;
        employee.Email = dto.Email;
        employee.Phone = dto.Phone;
        if (!string.IsNullOrEmpty(dto.Color))
            employee.Color = dto.Color;
        
        // Extended fields
        employee.DailyWorkHours = dto.DailyWorkHours;
        employee.WeekendWorkMode = dto.WeekendWorkMode;
        employee.SaturdayWorkHours = dto.SaturdayWorkHours;
        employee.PositionType = dto.PositionType;
        employee.AcademicTitle = dto.AcademicTitle;
        employee.ShiftScore = dto.ShiftScore;
        employee.IsNonHealthServices = dto.IsNonHealthServices;
        
        employee.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.EmployeeUpdated, 
            $"Personel güncellendi: {employee.FullName}", 
            "Employee", employee.Id,
            new { employee.FullName, employee.Title, employee.IdentityNo });
        
        return Json(new {
            employee.Id,
            employee.FullName,
            employee.Title,
            employee.IdentityNo,
            employee.Email,
            employee.Phone,
            employee.Color,
            employee.DailyWorkHours,
            employee.WeekendWorkMode,
            employee.SaturdayWorkHours,
            employee.PositionType,
            employee.AcademicTitle,
            employee.ShiftScore,
            employee.IsNonHealthServices,
            employee.UnitId
        });
    }

    [HttpDelete]
    [Route("api/employees/{id}")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organization.Id);
            
        if (employee == null)
            return NotFound();
            
        // Soft delete
        var employeeName = employee.FullName;
        employee.IsActive = false;
        employee.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.EmployeeDeleted, 
            $"Personel silindi: {employeeName}", 
            "Employee", id,
            new { FullName = employeeName });
        
        return Ok();
    }

    #endregion

    #region Shift API

    [HttpGet]
    [Route("api/shifts")]
    public async Task<IActionResult> GetShifts(int year, int month)
    {
        var organization = await GetOrCreateOrganizationAsync();
        
        // Get shifts for current month
        var shifts = await _context.Shifts
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .Select(s => new {
                s.Id,
                s.EmployeeId,
                date = s.Date.ToString("yyyy-MM-dd"),
                startTime = s.StartTime.ToString("HH:mm"),
                endTime = s.EndTime.ToString("HH:mm"),
                s.SpansNextDay,
                s.TotalHours,
                s.NightHours,
                s.IsWeekend,
                s.IsHoliday,
                s.OvernightHoursMode,
                templateId = s.ShiftTemplateId,
                templateName = s.ShiftTemplate != null ? s.ShiftTemplate.Name : null,
                templateColor = s.ShiftTemplate != null ? s.ShiftTemplate.Color : "#3B82F6",
                isPreviousMonth = false
            })
            .ToListAsync();
        
        // Get overnight shifts from previous month's last day
        var previousMonthLastDay = new DateOnly(year, month, 1).AddDays(-1);
        var previousMonthShifts = await _context.Shifts
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date == previousMonthLastDay && s.SpansNextDay)
            .Select(s => new {
                s.Id,
                s.EmployeeId,
                date = s.Date.ToString("yyyy-MM-dd"),
                startTime = s.StartTime.ToString("HH:mm"),
                endTime = s.EndTime.ToString("HH:mm"),
                s.SpansNextDay,
                s.TotalHours,
                s.NightHours,
                s.IsWeekend,
                s.IsHoliday,
                s.OvernightHoursMode,
                templateId = s.ShiftTemplateId,
                templateName = s.ShiftTemplate != null ? s.ShiftTemplate.Name : null,
                templateColor = s.ShiftTemplate != null ? s.ShiftTemplate.Color : "#3B82F6",
                isPreviousMonth = true
            })
            .ToListAsync();
        
        var allShifts = shifts.Concat(previousMonthShifts).ToList();
            
        return Json(allShifts);
    }

    [HttpPost]
    [Route("api/shifts")]
    public async Task<IActionResult> CreateShift([FromBody] ShiftDto dto)
    {
        var organization = await GetOrCreateOrganizationAsync();
        
        // Verify employee belongs to organization
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.OrganizationId == organization.Id);
            
        if (employee == null)
            return BadRequest("Employee not found");
        
        var date = DateOnly.Parse(dto.Date);
        var startTime = TimeOnly.Parse(dto.StartTime);
        var endTime = TimeOnly.Parse(dto.EndTime);
        
        // Check for overnight shift overlap - if this shift spans to next day, check if next day has a shift
        if (dto.SpansNextDay && !dto.IsDayOff)
        {
            var nextDay = date.AddDays(1);
            var nextDayShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.EmployeeId == dto.EmployeeId && s.Date == nextDay);
            
            if (nextDayShift != null)
            {
                return BadRequest(new { 
                    error = "overlap", 
                    message = "Ertesi günde nöbet var, üst üste binme oluşur",
                    nextDayDate = nextDay.ToString("yyyy-MM-dd")
                });
            }
        }
        
        // Check if previous day has an overnight shift that overlaps with this day
        var previousDay = date.AddDays(-1);
        var previousDayShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.EmployeeId == dto.EmployeeId && s.Date == previousDay && s.SpansNextDay);
        
        if (previousDayShift != null && !previousDayShift.IsDayOff)
        {
            return BadRequest(new { 
                error = "overlap", 
                message = "Önceki günün nöbeti bu güne sarkıyor, üst üste binme oluşur",
                previousDayDate = previousDay.ToString("yyyy-MM-dd")
            });
        }
        
        // Check if shift already exists for this day
        var existingShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.EmployeeId == dto.EmployeeId && s.Date == date);
            
        if (existingShift != null)
        {
            // Update existing
            existingShift.StartTime = startTime;
            existingShift.EndTime = endTime;
            existingShift.SpansNextDay = dto.SpansNextDay;
            existingShift.ShiftTemplateId = dto.ShiftTemplateId;
            existingShift.BreakMinutes = dto.BreakMinutes ?? organization.BreakMinutes;
            existingShift.IsDayOff = dto.IsDayOff;
            existingShift.OvernightHoursMode = dto.OvernightHoursMode; // FIX: Save overnight hours mode on update
            existingShift.UpdatedAt = DateTime.UtcNow;
            
            if (dto.IsDayOff) {
                existingShift.TotalHours = 0;
                existingShift.NightHours = 0;
            } else {
                CalculateShiftHours(existingShift, organization);
            }
            
            await _context.SaveChangesAsync();
            
            // Get updated employee totals
            var updateTotals = await GetEmployeeTotalsAsync(dto.EmployeeId, date.Year, date.Month);
            
            return Json(new { 
                id = existingShift.Id,
                totalHours = existingShift.TotalHours,
                isDayOff = existingShift.IsDayOff,
                employeeTotals = updateTotals
            });
        }
        
        var shift = new Shift
        {
            EmployeeId = dto.EmployeeId,
            Date = date,
            StartTime = startTime,
            EndTime = endTime,
            SpansNextDay = dto.SpansNextDay,
            ShiftTemplateId = dto.ShiftTemplateId,
            BreakMinutes = dto.BreakMinutes ?? organization.BreakMinutes,
            IsDayOff = dto.IsDayOff,
            OvernightHoursMode = dto.OvernightHoursMode
        };
        
        // Day off doesn't have hours
        if (dto.IsDayOff) {
            shift.TotalHours = 0;
            shift.NightHours = 0;
        } else {
            CalculateShiftHours(shift, organization);
        }
        
        _context.Shifts.Add(shift);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.ShiftCreated, 
            $"Nöbet eklendi: {employee.FullName} - {date:dd.MM.yyyy}", 
            "Shift", shift.Id,
            new { EmployeeName = employee.FullName, Date = date.ToString("yyyy-MM-dd"), StartTime = startTime.ToString(), EndTime = endTime.ToString() });
        
        // Get updated employee totals
        var totals = await GetEmployeeTotalsAsync(dto.EmployeeId, date.Year, date.Month);
        
        return Json(new { 
            id = shift.Id,
            totalHours = shift.TotalHours,
            isDayOff = shift.IsDayOff,
            employeeTotals = totals
        });
    }

    [HttpDelete]
    [Route("api/shifts/{id}")]
    public async Task<IActionResult> DeleteShift(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var shift = await _context.Shifts
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.Id == id && s.Employee.OrganizationId == organization.Id);
            
        if (shift == null)
            return NotFound();
        
        var employeeId = shift.EmployeeId;
        var year = shift.Date.Year;
        var month = shift.Date.Month;
        var employeeName = shift.Employee?.FullName ?? "Unknown";
        var shiftDate = shift.Date;
            
        _context.Shifts.Remove(shift);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.ShiftDeleted, 
            $"Nöbet silindi: {employeeName} - {shiftDate:dd.MM.yyyy}", 
            "Shift", id,
            new { EmployeeName = employeeName, Date = shiftDate.ToString("yyyy-MM-dd") });
        
        // Get updated employee totals
        var totals = await GetEmployeeTotalsAsync(employeeId, year, month);
        
        return Json(new { employeeTotals = totals });
    }

    [HttpDelete]
    [Route("api/shifts/employee/{employeeId}/date/{date}")]
    public async Task<IActionResult> DeleteShiftByEmployeeAndDate(int employeeId, string date)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var dateOnly = DateOnly.Parse(date);
        
        var shift = await _context.Shifts
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => 
                s.EmployeeId == employeeId && 
                s.Date == dateOnly && 
                s.Employee.OrganizationId == organization.Id);
            
        if (shift == null)
            return NotFound();
        
        // Store shift info before deleting
        var totalHours = shift.TotalHours;
        var isDayOff = shift.IsDayOff;
        var spansNextDay = shift.SpansNextDay;
        var year = dateOnly.Year;
        var month = dateOnly.Month;
            
        _context.Shifts.Remove(shift);
        await _context.SaveChangesAsync();
        
        // Get updated employee totals
        var totals = await GetEmployeeTotalsAsync(employeeId, year, month);
        
        return Json(new { 
            totalHours = totalHours,
            isDayOff = isDayOff,
            spansNextDay = spansNextDay,
            employeeTotals = totals
        });
    }

    #endregion

    #region Holiday API

    [HttpGet]
    [Route("api/holidays")]
    public async Task<IActionResult> GetHolidays(int year, int month)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id)
            .Where(h => h.Date.Year == year && h.Date.Month == month)
            .Select(h => new {
                h.Id,
                date = h.Date.ToString("yyyy-MM-dd"),
                h.Name,
                h.Type,
                h.IsHalfDay,
                h.HalfDayWorkHours
            })
            .ToListAsync();
            
        return Json(holidays);
    }

    [HttpPost]
    [Route("api/holidays")]
    public async Task<IActionResult> CreateHoliday([FromBody] HolidayDto dto)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var date = DateOnly.Parse(dto.Date);
        
        // Check if holiday already exists
        var existing = await _context.Holidays
            .FirstOrDefaultAsync(h => h.OrganizationId == organization.Id && h.Date == date);
            
        if (existing != null)
        {
            existing.Name = dto.Name;
            existing.Type = dto.Type;
            existing.IsHalfDay = dto.IsHalfDay;
            existing.HalfDayWorkHours = dto.IsHalfDay ? dto.HalfDayWorkHours : null;
        }
        else
        {
            var holiday = new Holiday
            {
                OrganizationId = organization.Id,
                Date = date,
                Name = dto.Name,
                Type = dto.Type,
                IsHalfDay = dto.IsHalfDay,
                HalfDayWorkHours = dto.IsHalfDay ? dto.HalfDayWorkHours : null
            };
            _context.Holidays.Add(holiday);
        }
        
        await _context.SaveChangesAsync();
        
        // Update all shifts on this date to mark as holiday
        var shifts = await _context.Shifts
            .Where(s => s.Employee.OrganizationId == organization.Id && s.Date == date)
            .ToListAsync();
            
        foreach (var shift in shifts)
        {
            shift.IsHoliday = true;
        }
        await _context.SaveChangesAsync();
        
        return Ok();
    }

    [HttpDelete]
    [Route("api/holidays/{id}")]
    public async Task<IActionResult> DeleteHoliday(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var holiday = await _context.Holidays
            .FirstOrDefaultAsync(h => h.Id == id && h.OrganizationId == organization.Id);
            
        if (holiday == null)
            return NotFound();
        
        var date = holiday.Date;
        _context.Holidays.Remove(holiday);
        
        // Update shifts to remove holiday flag
        var shifts = await _context.Shifts
            .Where(s => s.Employee.OrganizationId == organization.Id && s.Date == date)
            .ToListAsync();
            
        foreach (var shift in shifts)
        {
            shift.IsHoliday = false;
        }
        
        await _context.SaveChangesAsync();
        
        return Ok();
    }

    #endregion

    #region Shift Templates API

    [HttpGet]
    [Route("api/shift-templates")]
    public async Task<IActionResult> GetShiftTemplates()
    {
        var organization = await GetOrCreateOrganizationAsync();
        
        // Ensure default templates exist
        await CopyDefaultTemplatesToOrganization(organization.Id);
        
        var templates = await _context.ShiftTemplates
            .Where(t => t.OrganizationId == organization.Id)
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new {
                t.Id,
                t.Name,
                t.NameKey,
                startTime = t.StartTime.ToString("HH:mm"),
                endTime = t.EndTime.ToString("HH:mm"),
                t.SpansNextDay,
                t.BreakMinutes,
                t.Color,
                t.IsGlobal,
                canEdit = true // User can always edit their own templates
            })
            .ToListAsync();
            
        return Json(templates);
    }

    [HttpPost]
    [Route("api/shift-templates")]
    public async Task<IActionResult> CreateShiftTemplate([FromBody] ShiftTemplateDto dto)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
        }
        
        var organization = await GetOrCreateOrganizationAsync();
        
        var template = new ShiftTemplate
        {
            OrganizationId = organization.Id,
            Name = dto.Name,
            StartTime = TimeOnly.Parse(dto.StartTime),
            EndTime = TimeOnly.Parse(dto.EndTime),
            SpansNextDay = dto.SpansNextDay,
            BreakMinutes = dto.BreakMinutes,
            Color = dto.Color ?? "#3B82F6",
            IsGlobal = false,
            DisplayOrder = dto.DisplayOrder,
            IsActive = true
        };
        
        _context.ShiftTemplates.Add(template);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.ShiftTemplateCreated, 
            $"Vardiya şablonu eklendi: {template.Name}", 
            "ShiftTemplate", template.Id,
            new { template.Name, StartTime = template.StartTime.ToString(), EndTime = template.EndTime.ToString() });
        
        return Json(new {
            id = template.Id,
            name = template.Name,
            startTime = template.StartTime.ToString("HH:mm"),
            endTime = template.EndTime.ToString("HH:mm"),
            spansNextDay = template.SpansNextDay,
            breakMinutes = template.BreakMinutes,
            color = template.Color,
            isGlobal = false,
            canEdit = true
        });
    }

    [HttpPut]
    [Route("api/shift-templates/{id}")]
    public async Task<IActionResult> UpdateShiftTemplate(int id, [FromBody] ShiftTemplateDto dto)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
        }
        
        var organization = await GetOrCreateOrganizationAsync();
        var template = await _context.ShiftTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == organization.Id);
            
        if (template == null)
        {
            return NotFound(new { error = "Şablon bulunamadı" });
        }
        
        template.Name = dto.Name;
        template.StartTime = TimeOnly.Parse(dto.StartTime);
        template.EndTime = TimeOnly.Parse(dto.EndTime);
        template.SpansNextDay = dto.SpansNextDay;
        template.BreakMinutes = dto.BreakMinutes;
        template.Color = dto.Color ?? template.Color;
        template.DisplayOrder = dto.DisplayOrder;
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.ShiftTemplateUpdated, 
            $"Vardiya şablonu güncellendi: {template.Name}", 
            "ShiftTemplate", template.Id,
            new { template.Name, StartTime = template.StartTime.ToString(), EndTime = template.EndTime.ToString() });
        
        return Json(new {
            id = template.Id,
            name = template.Name,
            startTime = template.StartTime.ToString("HH:mm"),
            endTime = template.EndTime.ToString("HH:mm"),
            spansNextDay = template.SpansNextDay,
            breakMinutes = template.BreakMinutes,
            color = template.Color,
            isGlobal = false,
            canEdit = true
        });
    }

    [HttpDelete]
    [Route("api/shift-templates/{id}")]
    public async Task<IActionResult> DeleteShiftTemplate(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
        }
        
        var organization = await GetOrCreateOrganizationAsync();
        var template = await _context.ShiftTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == organization.Id);
            
        if (template == null)
        {
            return NotFound(new { error = "Şablon bulunamadı" });
        }
        
        var templateName = template.Name;
        
        // Soft delete
        template.IsActive = false;
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.ShiftTemplateDeleted, 
            $"Vardiya şablonu silindi: {templateName}", 
            "ShiftTemplate", id,
            new { Name = templateName });
        
        return Ok(new { success = true });
    }

    #endregion

    #region Leave API

    [HttpGet]
    [Route("api/leave-types")]
    public async Task<IActionResult> GetLeaveTypes()
    {
        var organization = await GetOrCreateOrganizationAsync();
        var leaveTypes = await _context.LeaveTypes
            .Where(lt => lt.OrganizationId == null || lt.OrganizationId == organization.Id)
            .Where(lt => lt.IsActive)
            .OrderBy(lt => lt.SortOrder)
            .Select(lt => new
            {
                lt.Id,
                lt.Code,
                NameTr = lt.NameTr,
                NameEn = lt.NameEn,
                lt.Category,
                lt.IsPaid,
                lt.DefaultDays,
                lt.Color,
                lt.IsSystem
            })
            .ToListAsync();
            
        return Ok(leaveTypes);
    }

    [HttpGet]
    [Route("api/leaves")]
    public async Task<IActionResult> GetLeaves(int year, int month)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var leaves = await _context.Leaves
            .Include(l => l.LeaveType)
            .Include(l => l.Employee)
            .Where(l => l.Employee.OrganizationId == organization.Id)
            .Where(l => l.Date.Year == year && l.Date.Month == month)
            .Select(l => new
            {
                l.Id,
                l.EmployeeId,
                l.LeaveTypeId,
                LeaveCode = l.LeaveType.Code,
                LeaveCodeEn = l.LeaveType.CodeEn,
                LeaveColor = l.LeaveType.Color,
                Date = l.Date.ToString("yyyy-MM-dd"),
                l.Notes
            })
            .ToListAsync();
            
        return Ok(leaves);
    }

    [HttpPost]
    [Route("api/leaves")]
    public async Task<IActionResult> CreateLeave([FromBody] CreateLeaveDto dto)
    {
        var organization = await GetOrCreateOrganizationAsync();
        
        // Verify employee belongs to organization
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.OrganizationId == organization.Id);
        if (employee == null)
            return NotFound(new { error = "Personel bulunamadı" });
            
        // Verify leave type
        var leaveType = await _context.LeaveTypes
            .FirstOrDefaultAsync(lt => lt.Id == dto.LeaveTypeId && (lt.OrganizationId == null || lt.OrganizationId == organization.Id));
        if (leaveType == null)
            return NotFound(new { error = "İzin türü bulunamadı" });
            
        if (!DateOnly.TryParse(dto.Date, out var date))
            return BadRequest(new { error = "Geçersiz tarih formatı" });
        
        // Check if leave already exists for this date
        var existingLeave = await _context.Leaves
            .FirstOrDefaultAsync(l => l.EmployeeId == dto.EmployeeId && l.Date == date);
        
        if (existingLeave != null)
        {
            // Update existing leave
            existingLeave.LeaveTypeId = dto.LeaveTypeId;
            existingLeave.Notes = dto.Notes;
            await _context.SaveChangesAsync();
            
            await _activityLog.LogAsync(ActivityType.LeaveUpdated, 
                $"İzin güncellendi: {employee.FullName} - {date:dd.MM.yyyy} ({leaveType.Code})", 
                "Leave", existingLeave.Id,
                new { EmployeeName = employee.FullName, Date = date.ToString("yyyy-MM-dd"), LeaveType = leaveType.Code });
            
            // Get updated employee totals
            var updateTotals = await GetEmployeeTotalsAsync(dto.EmployeeId, date.Year, date.Month);
            
            return Ok(new { 
                success = true, 
                id = existingLeave.Id,
                leaveCode = leaveType.Code,
                leaveCodeEn = leaveType.CodeEn,
                leaveColor = leaveType.Color,
                employeeTotals = updateTotals
            });
        }
        
        // Remove any shift on this date (leave replaces shift)
        var existingShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.EmployeeId == dto.EmployeeId && s.Date == date);
        if (existingShift != null)
        {
            _context.Shifts.Remove(existingShift);
        }
        
        var leave = new Leave
        {
            EmployeeId = dto.EmployeeId,
            LeaveTypeId = dto.LeaveTypeId,
            Date = date,
            Notes = dto.Notes
        };
        
        _context.Leaves.Add(leave);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.LeaveCreated, 
            $"İzin eklendi: {employee.FullName} - {date:dd.MM.yyyy} ({leaveType.Code})", 
            "Leave", leave.Id,
            new { EmployeeName = employee.FullName, Date = date.ToString("yyyy-MM-dd"), LeaveType = leaveType.Code });
        
        // Get updated employee totals
        var totals = await GetEmployeeTotalsAsync(dto.EmployeeId, date.Year, date.Month);
        
        return Ok(new { 
            success = true, 
            id = leave.Id,
            leaveCode = leaveType.Code,
            leaveCodeEn = leaveType.CodeEn,
            leaveColor = leaveType.Color,
            employeeTotals = totals
        });
    }

    [HttpDelete]
    [Route("api/leaves/{id}")]
    public async Task<IActionResult> DeleteLeave(int id)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var leave = await _context.Leaves
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == id && l.Employee.OrganizationId == organization.Id);
            
        if (leave == null)
            return NotFound(new { error = "İzin kaydı bulunamadı" });
        
        var employeeId = leave.EmployeeId;
        var year = leave.Date.Year;
        var month = leave.Date.Month;
        var employeeName = leave.Employee?.FullName ?? "Unknown";
        var leaveDate = leave.Date;
            
        _context.Leaves.Remove(leave);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.LeaveDeleted, 
            $"İzin silindi: {employeeName} - {leaveDate:dd.MM.yyyy}", 
            "Leave", id,
            new { EmployeeName = employeeName, Date = leaveDate.ToString("yyyy-MM-dd") });
        
        // Get updated employee totals
        var totals = await GetEmployeeTotalsAsync(employeeId, year, month);
        
        return Ok(new { success = true, employeeTotals = totals });
    }

    [HttpDelete]
    [Route("api/leaves/by-employee-date")]
    public async Task<IActionResult> DeleteLeaveByEmployeeAndDate(int employeeId, string date)
    {
        var organization = await GetOrCreateOrganizationAsync();
        
        if (!DateOnly.TryParse(date, out var parsedDate))
            return BadRequest(new { error = "Geçersiz tarih formatı" });
            
        var leave = await _context.Leaves
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.EmployeeId == employeeId && l.Date == parsedDate && l.Employee.OrganizationId == organization.Id);
            
        if (leave == null)
            return NotFound(new { error = "İzin kaydı bulunamadı" });
        
        var employeeName = leave.Employee?.FullName ?? "Unknown";
        var leaveId = leave.Id;
            
        _context.Leaves.Remove(leave);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.LeaveDeleted, 
            $"İzin silindi: {employeeName} - {parsedDate:dd.MM.yyyy}", 
            "Leave", leaveId,
            new { EmployeeName = employeeName, Date = parsedDate.ToString("yyyy-MM-dd") });
        
        // Get updated employee totals
        var totals = await GetEmployeeTotalsAsync(employeeId, parsedDate.Year, parsedDate.Month);
        
        return Ok(new { success = true, employeeTotals = totals });
    }

    /// <summary>
    /// Get employee totals (worked hours, required hours) for a specific month
    /// Called after shift operations to sync UI with backend calculations
    /// </summary>
    [HttpGet]
    [Route("api/employee/{employeeId}/totals")]
    public async Task<IActionResult> GetEmployeeTotals(int employeeId, int year, int month)
    {
        var organization = await GetOrCreateOrganizationAsync();
        
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.OrganizationId == organization.Id);
            
        if (employee == null)
            return NotFound(new { error = "Personel bulunamadı" });
        
        // Get shifts for this employee in the specified month
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        
        var shifts = await _context.Shifts
            .Include(s => s.ShiftTemplate)
            .Where(s => s.EmployeeId == employeeId && s.Date >= startDate && s.Date <= endDate)
            .ToListAsync();
        
        // Get leaves for this employee
        var leaves = await _context.Leaves
            .Where(l => l.EmployeeId == employeeId && l.Date >= startDate && l.Date <= endDate)
            .ToListAsync();
        
        // Get holidays
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id && h.Date >= startDate && h.Date <= endDate)
            .ToListAsync();
        
        // Get weekend days
        var weekendDays = organization.WeekendDays.Split(',').Select(int.Parse).ToList();
        
        // Get previous month's last shift for overnight calculation
        var prevMonthEnd = startDate.AddDays(-1);
        var prevMonthStart = new DateOnly(prevMonthEnd.Year, prevMonthEnd.Month, 1);
        var prevMonthLastShift = await _context.Shifts
            .Include(s => s.ShiftTemplate)
            .Where(s => s.EmployeeId == employeeId && s.Date >= prevMonthStart && s.Date <= prevMonthEnd)
            .OrderByDescending(s => s.Date)
            .FirstOrDefaultAsync();
        
        // Calculate required hours
        var requiredHours = CalculateRequiredHours(employee, year, month, holidays, weekendDays, leaves);
        
        // Calculate worked hours
        decimal workedHours = 0;
        int dayOffCount = 0;
        
        // Add spillover from previous month overnight shift
        if (prevMonthLastShift != null && !prevMonthLastShift.IsDayOff && prevMonthLastShift.SpansNextDay && prevMonthLastShift.OvernightHoursMode == 0)
        {
            var spilledHours = CalculateHoursAfterMidnight(prevMonthLastShift);
            workedHours += spilledHours;
        }
        
        foreach (var shift in shifts)
        {
            if (shift.IsDayOff)
            {
                dayOffCount++;
                continue;
            }
            
            // Handle overnight mode - don't double count
            if (shift.SpansNextDay && shift.OvernightHoursMode == 0)
            {
                // Split mode: only count hours until midnight
                var hoursBeforeMidnight = CalculateHoursBeforeMidnight(shift);
                workedHours += hoursBeforeMidnight;
            }
            else if (shift.TotalHours > 0)
            {
                // Use pre-calculated total hours
                workedHours += shift.TotalHours;
            }
            else
            {
                // Fallback calculation if TotalHours is 0
                var hours = CalculateShiftHours(shift);
                workedHours += hours;
            }
        }
        
        // Subtract day offs from required hours
        requiredHours = Math.Max(0, requiredHours - (dayOffCount * employee.DailyWorkHours));
        
        var diff = workedHours - requiredHours;
        
        return Json(new {
            employeeId = employeeId,
            workedHours = Math.Round(workedHours, 1),
            requiredHours = Math.Round(requiredHours, 1),
            diff = Math.Round(diff, 1),
            dayOffCount = dayOffCount
        });
    }
    
    /// <summary>
    /// Calculate hours before midnight for split overnight shifts
    /// </summary>
    private decimal CalculateHoursBeforeMidnight(Shift shift)
    {
        if (!shift.SpansNextDay)
            return shift.TotalHours;
        
        var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
        var midnightMinutes = 24 * 60;
        var minutesBeforeMidnight = midnightMinutes - startMinutes;
        
        // Subtract break if applicable (assume half before midnight)
        var breakMinutes = shift.BreakMinutes > 0 ? shift.BreakMinutes : (shift.ShiftTemplate?.BreakMinutes ?? 0);
        minutesBeforeMidnight -= breakMinutes / 2;
        
        return Math.Max(0, minutesBeforeMidnight / 60m);
    }
    
    /// <summary>
    /// Calculate basic shift hours (fallback when TotalHours is 0)
    /// </summary>
    private decimal CalculateShiftHours(Shift shift)
    {
        var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
        
        if (shift.SpansNextDay || endMinutes < startMinutes)
            endMinutes += 24 * 60;
        
        var totalMinutes = endMinutes - startMinutes;
        var breakMinutes = shift.BreakMinutes > 0 ? shift.BreakMinutes : (shift.ShiftTemplate?.BreakMinutes ?? 0);
        totalMinutes -= breakMinutes;
        
        return Math.Max(0, totalMinutes / 60m);
    }

    #endregion

    #region Saved Schedules API (Registered Users Only)

    [HttpGet]
    [Route("api/saved-schedules")]
    public async Task<IActionResult> GetSavedSchedules(int? year, int? month)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
        }
        
        var organization = await GetOrCreateOrganizationAsync();
        
        var query = _context.SavedSchedules
            .Where(s => s.OrganizationId == organization.Id);
        
        if (year.HasValue)
            query = query.Where(s => s.Year == year.Value);
        if (month.HasValue)
            query = query.Where(s => s.Month == month.Value);
        
        var schedules = await query
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new {
                s.Id,
                s.Name,
                s.Year,
                s.Month,
                s.Description,
                createdAt = s.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                updatedAt = s.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
            })
            .ToListAsync();
            
        return Json(schedules);
    }

    [HttpGet]
    [Route("api/saved-schedules/{id}")]
    public async Task<IActionResult> GetSavedSchedule(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
        }
        
        var organization = await GetOrCreateOrganizationAsync();
        var schedule = await _context.SavedSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
            
        if (schedule == null)
            return NotFound();
        
        return Json(new {
            schedule.Id,
            schedule.Name,
            schedule.Year,
            schedule.Month,
            schedule.Description,
            schedule.ShiftDataJson,
            createdAt = schedule.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            updatedAt = schedule.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
        });
    }

    [HttpPost]
    [Route("api/saved-schedules")]
    public async Task<IActionResult> SaveSchedule([FromBody] SaveScheduleDto dto)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
        }
        
        var organization = await GetOrCreateOrganizationAsync();
        
        // Get current shifts for the specified month
        var shifts = await _context.Shifts
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == dto.Year && s.Date.Month == dto.Month)
            .Select(s => new {
                s.EmployeeId,
                date = s.Date.ToString("yyyy-MM-dd"),
                startTime = s.StartTime.ToString("HH:mm"),
                endTime = s.EndTime.ToString("HH:mm"),
                s.SpansNextDay,
                s.BreakMinutes,
                s.TotalHours,
                s.NightHours,
                s.IsWeekend,
                s.IsHoliday,
                s.IsDayOff,
                s.OvernightHoursMode,
                s.ShiftTemplateId
            })
            .ToListAsync();
        
        var shiftDataJson = System.Text.Json.JsonSerializer.Serialize(shifts);
        
        var savedSchedule = new SavedSchedule
        {
            OrganizationId = organization.Id,
            Name = dto.Name,
            Year = dto.Year,
            Month = dto.Month,
            Description = dto.Description,
            ShiftDataJson = shiftDataJson
        };
        
        _context.SavedSchedules.Add(savedSchedule);
        await _context.SaveChangesAsync();
        
        return Json(new {
            savedSchedule.Id,
            savedSchedule.Name,
            savedSchedule.Year,
            savedSchedule.Month,
            message = "Nöbet listesi kaydedildi"
        });
    }

    [HttpPut]
    [Route("api/saved-schedules/{id}")]
    public async Task<IActionResult> UpdateSavedSchedule(int id, [FromBody] SaveScheduleDto dto)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
        }
        
        var organization = await GetOrCreateOrganizationAsync();
        var schedule = await _context.SavedSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
            
        if (schedule == null)
            return NotFound();
        
        schedule.Name = dto.Name;
        schedule.Description = dto.Description;
        schedule.UpdatedAt = DateTime.UtcNow;
        
        // If updateShifts is true, also update the shift data
        if (dto.UpdateShiftData)
        {
            var shifts = await _context.Shifts
                .Include(s => s.ShiftTemplate)
                .Where(s => s.Employee.OrganizationId == organization.Id)
                .Where(s => s.Date.Year == dto.Year && s.Date.Month == dto.Month)
                .Select(s => new {
                    s.EmployeeId,
                    date = s.Date.ToString("yyyy-MM-dd"),
                    startTime = s.StartTime.ToString("HH:mm"),
                    endTime = s.EndTime.ToString("HH:mm"),
                    s.SpansNextDay,
                    s.BreakMinutes,
                    s.TotalHours,
                    s.NightHours,
                    s.IsWeekend,
                    s.IsHoliday,
                    s.IsDayOff,
                    s.OvernightHoursMode,
                    s.ShiftTemplateId
                })
                .ToListAsync();
            
            schedule.ShiftDataJson = System.Text.Json.JsonSerializer.Serialize(shifts);
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Nöbet listesi güncellendi" });
    }

    [HttpPost]
    [Route("api/saved-schedules/{id}/load")]
    public async Task<IActionResult> LoadSavedSchedule(int id)
    {
        try
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
            }
            
            var organization = await GetOrCreateOrganizationAsync();
            var schedule = await _context.SavedSchedules
                .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
                
            if (schedule == null)
                return NotFound(new { error = "Kayıtlı liste bulunamadı" });
            
            // Parse the saved shift data (use case-insensitive option since JSON uses camelCase)
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            List<SavedShiftData>? savedShifts;
            try
            {
                savedShifts = System.Text.Json.JsonSerializer.Deserialize<List<SavedShiftData>>(schedule.ShiftDataJson, jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize saved schedule data for schedule {ScheduleId}", id);
                return BadRequest(new { error = "Kaydedilmiş veri formatı hatalı" });
            }
            
            if (savedShifts == null)
            {
                return BadRequest(new { error = "Kaydedilmiş veri okunamadı" });
            }
            
            // Get valid employee IDs for this organization
            var validEmployeeIds = await _context.Employees
                .Where(e => e.OrganizationId == organization.Id && e.IsActive)
                .Select(e => e.Id)
                .ToListAsync();
            
            // Delete existing shifts for this month
            var existingShifts = await _context.Shifts
                .Where(s => s.Employee.OrganizationId == organization.Id)
                .Where(s => s.Date.Year == schedule.Year && s.Date.Month == schedule.Month)
                .ToListAsync();
            
            _context.Shifts.RemoveRange(existingShifts);
            
            // Create new shifts from saved data
            var newShifts = new List<Shift>();
            foreach (var savedShift in savedShifts)
            {
                // Only restore shifts for employees that still exist
                if (!validEmployeeIds.Contains(savedShift.EmployeeId))
                    continue;
                
                var shift = new Shift
                {
                    EmployeeId = savedShift.EmployeeId,
                    Date = DateOnly.Parse(savedShift.Date),
                    StartTime = TimeOnly.Parse(savedShift.StartTime),
                    EndTime = TimeOnly.Parse(savedShift.EndTime),
                    SpansNextDay = savedShift.SpansNextDay,
                    BreakMinutes = savedShift.BreakMinutes,
                    TotalHours = savedShift.TotalHours,
                    NightHours = savedShift.NightHours,
                    IsWeekend = savedShift.IsWeekend,
                    IsHoliday = savedShift.IsHoliday,
                    IsDayOff = savedShift.IsDayOff,
                    OvernightHoursMode = savedShift.OvernightHoursMode,
                    ShiftTemplateId = savedShift.ShiftTemplateId
                };
                newShifts.Add(shift);
            }
            
            _context.Shifts.AddRange(newShifts);
            await _context.SaveChangesAsync();
            
            return Json(new { 
                message = $"'{schedule.Name}' nöbet listesi yüklendi",
                shiftCount = newShifts.Count,
                year = schedule.Year,
                month = schedule.Month
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading saved schedule {ScheduleId}", id);
            return StatusCode(500, new { error = "Yükleme sırasında bir hata oluştu" });
        }
    }

    [HttpDelete]
    [Route("api/saved-schedules/{id}")]
    public async Task<IActionResult> DeleteSavedSchedule(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { error = "Bu özellik için giriş yapmanız gerekiyor" });
        }
        
        var organization = await GetOrCreateOrganizationAsync();
        var schedule = await _context.SavedSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organization.Id);
            
        if (schedule == null)
            return NotFound();
        
        _context.SavedSchedules.Remove(schedule);
        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Nöbet listesi silindi" });
    }

    #endregion

    #region Payroll

    /// <summary>
    /// Payroll/Timesheet page - only for registered users
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Payroll(int? year, int? month, int? nightStartHour, int? nightEndHour, string? source, bool calculate = false, int? loadId = null, int? unitId = null)
    {
        // Only registered users can access payroll
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Payroll") });
        }
        
        // Check if user has payroll access
        var (_, canAccessPayroll) = await GetFeatureAccessAsync();
        if (!canAccessPayroll)
        {
            TempData["Error"] = "Puantaj özelliğine erişim yetkiniz bulunmamaktadır.";
            return RedirectToAction("Index");
        }

        var selectedYear = year ?? DateTime.Now.Year;
        var selectedMonth = month ?? DateTime.Now.Month;
        var dataSource = source ?? "shift"; // "shift" or "attendance"
        
        var organization = await GetOrCreateOrganizationAsync();
        var isPremium = await IsPremiumUserAsync();
        
        // Use custom night hours or organization defaults
        var nightStart = nightStartHour.HasValue 
            ? new TimeOnly(nightStartHour.Value, 0) 
            : organization.NightStartTime;
        var nightEnd = nightEndHour.HasValue 
            ? new TimeOnly(nightEndHour.Value, 0) 
            : organization.NightEndTime;
        
        // Load units for premium users
        var units = new List<Unit>();
        if (isPremium)
        {
            try
            {
                // Initialize defaults if needed (same as Index)
                await InitializeDefaultUnitTypesAsync(organization.Id);
                await InitializeDefaultUnitAsync(organization.Id);
                
                units = await _context.Units
                    .Where(u => u.OrganizationId == organization.Id && u.IsActive)
                    .OrderBy(u => u.SortOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load units for Payroll page");
            }
        }
        
        // Build employee query with optional unit filter
        var employeeQuery = _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive);
            
        if (isPremium && unitId.HasValue)
        {
            employeeQuery = employeeQuery.Where(e => e.UnitId == unitId.Value);
        }
        
        var employees = await employeeQuery
            .OrderBy(e => e.FullName)
            .ToListAsync();
        
        var employeeIds = employees.Select(e => e.Id).ToList();
            
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id)
            .Where(h => h.Date.Year == selectedYear && h.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get shifts for current month (filtered by employees)
        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => employeeIds.Contains(s.EmployeeId))
            .Where(s => s.Date.Year == selectedYear && s.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get attendance for current month (filtered by employees)
        var attendances = await _context.TimeAttendances
            .Include(a => a.Employee)
            .Where(a => employeeIds.Contains(a.EmployeeId))
            .Where(a => a.Date.Year == selectedYear && a.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get leaves for current month (filtered by employees)
        var leaves = await _context.Leaves
            .Include(l => l.Employee)
            .Include(l => l.LeaveType)
            .Where(l => employeeIds.Contains(l.EmployeeId))
            .Where(l => l.Date.Year == selectedYear && l.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get overnight shifts from previous month (filtered by employees)
        var previousMonthLastDay = new DateOnly(selectedYear, selectedMonth, 1).AddDays(-1);
        var previousMonthShifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => employeeIds.Contains(s.EmployeeId))
            .Where(s => s.Date == previousMonthLastDay && s.SpansNextDay)
            .ToListAsync();
        
        // Get saved payrolls for this month
        var savedPayrolls = await _context.SavedPayrolls
            .Where(p => p.OrganizationId == organization.Id && p.Year == selectedYear && p.Month == selectedMonth)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var viewModel = new PayrollViewModel
        {
            Organization = organization,
            Employees = employees,
            Holidays = holidays,
            Shifts = shifts,
            Attendances = attendances,
            PreviousMonthOvernightShifts = previousMonthShifts,
            Leaves = leaves,
            SavedPayrolls = savedPayrolls,
            Units = units,
            SelectedUnitId = unitId,
            IsPremium = isPremium,
            SelectedYear = selectedYear,
            SelectedMonth = selectedMonth,
            NightStartTime = nightStart,
            NightEndTime = nightEnd,
            DataSource = dataSource,
            IsCalculated = calculate || loadId.HasValue
        };

        // Load saved payroll if loadId is provided
        if (loadId.HasValue)
        {
            var savedPayroll = await _context.SavedPayrolls
                .FirstOrDefaultAsync(p => p.Id == loadId.Value && p.OrganizationId == organization.Id);
            
            if (savedPayroll != null)
            {
                viewModel.LoadedPayrollId = savedPayroll.Id;
                viewModel.LoadedPayrollName = savedPayroll.Name;
                viewModel.DataSource = savedPayroll.DataSource;
                viewModel.NightStartTime = new TimeOnly(savedPayroll.NightStartHour, 0);
                viewModel.NightEndTime = new TimeOnly(savedPayroll.NightEndHour, 0);
                
                // Parse saved payroll data
                var savedEntries = System.Text.Json.JsonSerializer.Deserialize<List<SavedPayrollEntry>>(savedPayroll.PayrollDataJson) 
                    ?? new List<SavedPayrollEntry>();
                
                // Convert to EmployeePayroll objects
                viewModel.EmployeePayrolls = savedEntries.Select(e => new EmployeePayroll
                {
                    Employee = employees.FirstOrDefault(emp => emp.Id == e.EmployeeId) ?? new Employee { Id = e.EmployeeId, FullName = e.EmployeeName, Title = e.EmployeeTitle },
                    WorkedDays = e.WorkedDays,
                    TotalWorkedHours = e.TotalWorkedHours,
                    RequiredHours = e.RequiredHours,
                    NightHours = e.NightHours,
                    WeekendHours = e.WeekendHours,
                    HolidayHours = e.HolidayHours,
                    DayOffCount = e.DayOffCount,
                    ShiftDetails = e.DailyEntries?.Select(d => new ShiftDetail
                    {
                        Date = DateOnly.TryParse(d.Date, out var dt) ? dt : default,
                        StartTime = TimeOnly.TryParse(d.StartTime, out var st) ? st : null,
                        EndTime = TimeOnly.TryParse(d.EndTime, out var et) ? et : null,
                        TotalHours = d.Hours,
                        NightHours = d.NightHours,
                        IsWeekend = d.IsWeekend,
                        IsHoliday = d.IsHoliday,
                        IsDayOff = d.IsDayOff,
                        Note = d.Note
                    }).ToList() ?? new List<ShiftDetail>()
                }).ToList();
            }
        }
        // Only calculate if requested and not loading saved
        else if (calculate)
        {
            if (dataSource == "attendance")
            {
                viewModel.EmployeePayrolls = CalculatePayrollFromAttendance(
                    employees, attendances, holidays, leaves, organization, selectedYear, selectedMonth, nightStart, nightEnd);
            }
            else
            {
                viewModel.EmployeePayrolls = CalculateEmployeePayrolls(
                    employees, shifts, previousMonthShifts, holidays, leaves,
                    organization, selectedYear, selectedMonth, nightStart, nightEnd);
            }
        }

        return View(viewModel);
    }
    
    /// <summary>
    /// Calculate payroll from time attendance records
    /// </summary>
    private List<EmployeePayroll> CalculatePayrollFromAttendance(
        List<Employee> employees,
        List<TimeAttendance> attendances,
        List<Holiday> holidays,
        List<Leave> leaves,
        Organization organization,
        int year, int month,
        TimeOnly nightStart, TimeOnly nightEnd)
    {
        var payrolls = new List<EmployeePayroll>();
        var weekendDays = organization.WeekendDays.Split(',').Select(int.Parse).ToList();

        foreach (var employee in employees)
        {
            var employeeAttendances = attendances.Where(a => a.EmployeeId == employee.Id).ToList();
            var employeeLeaves = leaves.Where(l => l.EmployeeId == employee.Id).ToList();
            
            var payroll = new EmployeePayroll
            {
                Employee = employee,
                ShiftDetails = new List<ShiftDetail>()
            };

            // Calculate required hours for employee (leaves reduce required hours)
            payroll.RequiredHours = CalculateRequiredHours(employee, year, month, holidays, weekendDays, employeeLeaves);

            foreach (var att in employeeAttendances.OrderBy(a => a.Date))
            {
                var holiday = holidays.FirstOrDefault(h => h.Date == att.Date);
                var isWeekend = weekendDays.Contains((int)att.Date.DayOfWeek);

                var detail = new ShiftDetail
                {
                    Date = att.Date,
                    StartTime = att.CheckInTime,
                    EndTime = att.CheckOutTime,
                    SpansNextDay = att.CheckOutToNextDay,
                    IsDayOff = att.Type == AttendanceType.DayOff,
                    IsWeekend = isWeekend,
                    IsHoliday = holiday != null,
                    HolidayName = holiday?.Name,
                    Note = att.Notes
                };

                if (att.Type == AttendanceType.DayOff)
                {
                    payroll.DayOffCount++;
                }
                else if (att.WorkedHours.HasValue && att.WorkedHours > 0)
                {
                    payroll.WorkedDays++;
                    detail.TotalHours = att.WorkedHours.Value;
                    payroll.TotalWorkedHours += att.WorkedHours.Value;

                    // Calculate night hours from attendance
                    if (att.CheckInTime.HasValue && att.CheckOutTime.HasValue)
                    {
                        var nightHours = CalculateFullNightHours(att.CheckInTime.Value, att.CheckOutTime.Value, 
                            att.CheckOutToNextDay, nightStart, nightEnd, 0);
                        detail.NightHours = nightHours;
                        payroll.NightHours += nightHours;
                    }

                    if (isWeekend)
                        payroll.WeekendHours += att.WorkedHours.Value;

                    if (holiday != null)
                        payroll.HolidayHours += att.WorkedHours.Value;
                }

                payroll.ShiftDetails.Add(detail);
            }
            
            // Add leave days to ShiftDetails
            foreach (var leave in employeeLeaves)
            {
                // Skip if already have an attendance record for this date
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
            
            // Sort ShiftDetails by date
            payroll.ShiftDetails = payroll.ShiftDetails.OrderBy(d => d.Date).ToList();

            payrolls.Add(payroll);
        }

        return payrolls;
    }

    /// <summary>
    /// Calculate payroll data for all employees
    /// </summary>
    private List<EmployeePayroll> CalculateEmployeePayrolls(
        List<Employee> employees,
        List<Shift> shifts,
        List<Shift> previousMonthShifts,
        List<Holiday> holidays,
        List<Leave> leaves,
        Organization organization,
        int year, int month,
        TimeOnly nightStart, TimeOnly nightEnd)
    {
        var payrolls = new List<EmployeePayroll>();
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var weekendDays = organization.WeekendDays.Split(',').Select(int.Parse).ToList();

        foreach (var employee in employees)
        {
            var employeeShifts = shifts.Where(s => s.EmployeeId == employee.Id).ToList();
            var employeeLeaves = leaves.Where(l => l.EmployeeId == employee.Id).ToList();
            var prevMonthShift = previousMonthShifts.FirstOrDefault(s => s.EmployeeId == employee.Id);
            
            var payroll = new EmployeePayroll
            {
                Employee = employee,
                ShiftDetails = new List<ShiftDetail>()
            };

            // Calculate required hours for employee (leaves reduce required hours)
            payroll.RequiredHours = CalculateRequiredHours(employee, year, month, holidays, weekendDays, employeeLeaves);

            // Add hours from overnight shift from previous month (if split mode)
            if (prevMonthShift != null && !prevMonthShift.IsDayOff && prevMonthShift.OvernightHoursMode == 0)
            {
                var spilledHours = CalculateHoursAfterMidnight(prevMonthShift);
                var spilledNightHours = CalculateNightHoursAfterMidnight(prevMonthShift, nightStart, nightEnd);
                payroll.TotalWorkedHours += spilledHours;
                payroll.NightHours += spilledNightHours;
            }

            // Process each shift
            foreach (var shift in employeeShifts)
            {
                var holiday = holidays.FirstOrDefault(h => h.Date == shift.Date);
                var isWeekend = weekendDays.Contains((int)shift.Date.DayOfWeek);

                var detail = new ShiftDetail
                {
                    Date = shift.Date,
                    StartTime = shift.StartTime,
                    EndTime = shift.EndTime,
                    SpansNextDay = shift.SpansNextDay,
                    IsDayOff = shift.IsDayOff,
                    IsWeekend = isWeekend,
                    IsHoliday = holiday != null,
                    HolidayName = holiday?.Name
                };

                if (shift.IsDayOff)
                {
                    payroll.DayOffCount++;
                }
                else
                {
                    payroll.WorkedDays++;
                    
                    // Calculate hours for this month (considering overnight mode)
                    var hoursThisMonth = CalculateShiftHoursForMonth(shift, year, month);
                    detail.TotalHours = hoursThisMonth;
                    payroll.TotalWorkedHours += hoursThisMonth;

                    // Calculate night hours with custom night time range
                    var nightHours = CalculateNightHours(shift, nightStart, nightEnd, year, month);
                    detail.NightHours = nightHours;
                    payroll.NightHours += nightHours;

                    // Weekend hours
                    if (isWeekend)
                    {
                        payroll.WeekendHours += hoursThisMonth;
                    }

                    // Holiday hours
                    if (holiday != null)
                    {
                        payroll.HolidayHours += hoursThisMonth;
                    }
                }

                payroll.ShiftDetails.Add(detail);
            }
            
            // Add leave days to ShiftDetails
            foreach (var leave in employeeLeaves)
            {
                // Skip if already have a shift record for this date
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
            
            // Sort ShiftDetails by date
            payroll.ShiftDetails = payroll.ShiftDetails.OrderBy(d => d.Date).ToList();

            payrolls.Add(payroll);
        }

        return payrolls;
    }

    /// <summary>
    /// Calculate total work days for an employee in a month
    /// </summary>
    private int CalculateTotalWorkDays(Employee employee, int year, int month, List<Holiday> holidays, List<int> weekendDays)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        int workDays = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var dayOfWeek = date.DayOfWeek;
            var isSaturday = dayOfWeek == DayOfWeek.Saturday;
            var isSunday = dayOfWeek == DayOfWeek.Sunday;
            var isWeekend = weekendDays.Contains((int)dayOfWeek);
            
            var holiday = holidays.FirstOrDefault(h => h.Date == date);
            
            // Full holiday - not a work day
            if (holiday != null && !holiday.IsHalfDay)
                continue;

            if (isWeekend)
            {
                // Check weekend work mode
                switch (employee.WeekendWorkMode)
                {
                    case 1: // Works both days
                        workDays++;
                        break;
                    case 2: // Only Saturday
                    case 3: // Saturday specific hours
                        if (isSaturday) workDays++;
                        break;
                }
            }
            else
            {
                workDays++;
            }
        }

        return workDays;
    }

    /// <summary>
    /// Calculate required work hours for an employee
    /// Leave on weekends only reduces hours if the employee actually works weekends
    /// </summary>
    private decimal CalculateRequiredHours(Employee employee, int year, int month, List<Holiday> holidays, List<int> weekendDays, List<Leave>? leaves = null)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        decimal requiredHours = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var dayOfWeek = date.DayOfWeek;
            var isSaturday = dayOfWeek == DayOfWeek.Saturday;
            var isWeekend = weekendDays.Contains((int)dayOfWeek);
            var hasLeaveOnThisDay = leaves != null && leaves.Any(l => l.Date == date);
            
            var holiday = holidays.FirstOrDefault(h => h.Date == date);
            
            // Full holiday - no work required
            if (holiday != null && !holiday.IsHalfDay)
                continue;
            
            // Half-day holiday
            if (holiday != null && holiday.IsHalfDay && holiday.HalfDayWorkHours.HasValue)
            {
                // Only add half-day hours if employee should work this day AND no leave
                if ((!isWeekend || employee.WeekendWorkMode > 0) && !hasLeaveOnThisDay)
                {
                    requiredHours += holiday.HalfDayWorkHours.Value;
                }
                continue;
            }

            if (isWeekend)
            {
                // Weekend work depends on WeekendWorkMode
                switch (employee.WeekendWorkMode)
                {
                    case 0: // Doesn't work weekends
                        // Leave on weekend doesn't affect required hours (already 0 for this day)
                        break;
                    case 1: // Works both Saturday and Sunday
                        if (!hasLeaveOnThisDay)
                            requiredHours += employee.DailyWorkHours;
                        break;
                    case 2: // Only Saturday (full day)
                        if (isSaturday && !hasLeaveOnThisDay)
                            requiredHours += employee.DailyWorkHours;
                        // Sunday leave doesn't affect (doesn't work Sundays)
                        break;
                    case 3: // Saturday specific hours
                        if (isSaturday && employee.SaturdayWorkHours.HasValue && !hasLeaveOnThisDay)
                            requiredHours += employee.SaturdayWorkHours.Value;
                        // Sunday leave doesn't affect (doesn't work Sundays)
                        break;
                }
            }
            else
            {
                // Weekday - leave reduces required hours
                if (!hasLeaveOnThisDay)
                    requiredHours += employee.DailyWorkHours;
            }
        }

        // Ensure required hours never goes below zero
        return Math.Max(0, requiredHours);
    }

    /// <summary>
    /// Get employee totals for a specific month (worked hours, required hours, diff)
    /// Used to return accurate totals after shift operations
    /// </summary>
    private async Task<object> GetEmployeeTotalsAsync(int employeeId, int year, int month)
    {
        var organization = await GetOrCreateOrganizationAsync();
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.OrganizationId == organization.Id);
            
        if (employee == null)
            return new { workedHours = 0m, requiredHours = 0m, diff = 0m, dayOffCount = 0 };
        
        // Get all shifts for the month
        var shifts = await _context.Shifts
            .Where(s => s.EmployeeId == employeeId && s.Date.Year == year && s.Date.Month == month)
            .ToListAsync();
        
        // Get leaves for the month
        var leaves = await _context.Leaves
            .Where(l => l.EmployeeId == employeeId && l.Date.Year == year && l.Date.Month == month)
            .ToListAsync();
        
        // Get holidays
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id && h.Date.Year == year && h.Date.Month == month)
            .ToListAsync();
        
        var weekendDays = organization.WeekendDays.Split(',').Select(int.Parse).ToList();
        
        // Calculate worked hours
        decimal workedHours = 0;
        int dayOffCount = 0;
        
        foreach (var shift in shifts)
        {
            if (shift.IsDayOff)
            {
                dayOffCount++;
            }
            else
            {
                workedHours += shift.TotalHours;
            }
        }
        
        // Get previous month's overnight shift spillover
        var prevMonth = month == 1 ? 12 : month - 1;
        var prevYear = month == 1 ? year - 1 : year;
        var lastDayPrevMonth = DateTime.DaysInMonth(prevYear, prevMonth);
        var prevMonthDate = new DateOnly(prevYear, prevMonth, lastDayPrevMonth);
        
        var prevMonthShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && s.Date == prevMonthDate && s.SpansNextDay && !s.IsDayOff && s.OvernightHoursMode == 0);
        
        if (prevMonthShift != null)
        {
            var spilledHours = CalculateHoursAfterMidnight(prevMonthShift);
            workedHours += spilledHours;
        }
        
        // Calculate required hours (considers leaves, holidays, weekend mode, day offs)
        var requiredHours = CalculateRequiredHours(employee, year, month, holidays, weekendDays, leaves);
        
        // Subtract day off days from required hours
        requiredHours -= dayOffCount * employee.DailyWorkHours;
        requiredHours = Math.Max(0, requiredHours);
        
        var diff = workedHours - requiredHours;
        
        return new {
            workedHours = Math.Round(workedHours, 1),
            requiredHours = Math.Round(requiredHours, 1),
            diff = Math.Round(diff, 1),
            dayOffCount = dayOffCount
        };
    }

    /// <summary>
    /// Calculate night hours with custom night time range
    /// </summary>
    private decimal CalculateNightHours(Shift shift, TimeOnly nightStart, TimeOnly nightEnd, int year, int month)
    {
        if (shift.IsDayOff) return 0;

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lastDayOfMonth = new DateOnly(year, month, daysInMonth);
        bool spansToNextMonth = shift.SpansNextDay && shift.Date == lastDayOfMonth;

        // For shifts spanning to next month with split mode, only count hours before midnight
        if (spansToNextMonth && shift.OvernightHoursMode == 0)
        {
            return CalculateNightHoursBeforeMidnight(shift, nightStart, nightEnd);
        }

        // Calculate night hours for the full shift
        return CalculateFullNightHours(shift.StartTime, shift.EndTime, shift.SpansNextDay, nightStart, nightEnd, shift.BreakMinutes);
    }

    /// <summary>
    /// Calculate night hours for a full shift
    /// </summary>
    private decimal CalculateFullNightHours(TimeOnly start, TimeOnly end, bool spansNextDay, TimeOnly nightStart, TimeOnly nightEnd, int breakMinutes)
    {
        decimal nightMinutes = 0;

        if (spansNextDay)
        {
            // Shift spans midnight
            // Part 1: Start to midnight
            nightMinutes += GetNightMinutesInRange(start, new TimeOnly(23, 59, 59), nightStart, nightEnd);
            // Part 2: Midnight to end
            nightMinutes += GetNightMinutesInRange(new TimeOnly(0, 0), end, nightStart, nightEnd);
        }
        else
        {
            // Regular shift within same day
            nightMinutes += GetNightMinutesInRange(start, end, nightStart, nightEnd);
        }

        // Proportionally subtract break time from night hours
        var totalMinutes = spansNextDay
            ? (24 * 60 - start.Hour * 60 - start.Minute) + (end.Hour * 60 + end.Minute)
            : (end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute);
        
        if (totalMinutes > 0 && breakMinutes > 0)
        {
            var nightRatio = nightMinutes / totalMinutes;
            nightMinutes -= breakMinutes * nightRatio;
        }

        return Math.Max(0, nightMinutes / 60m);
    }

    /// <summary>
    /// Get night minutes within a time range
    /// </summary>
    private decimal GetNightMinutesInRange(TimeOnly start, TimeOnly end, TimeOnly nightStart, TimeOnly nightEnd)
    {
        decimal nightMinutes = 0;

        // Night typically spans midnight (e.g., 22:00 - 06:00)
        // We need to handle two periods:
        // Period 1: nightStart (22:00) to midnight (24:00)
        // Period 2: midnight (00:00) to nightEnd (06:00)

        var startMinutes = start.Hour * 60 + start.Minute;
        var endMinutes = end.Hour * 60 + end.Minute;
        var nightStartMinutes = nightStart.Hour * 60 + nightStart.Minute;
        var nightEndMinutes = nightEnd.Hour * 60 + nightEnd.Minute;

        // If night end is before night start (spans midnight)
        if (nightEndMinutes < nightStartMinutes)
        {
            // Period 1: nightStart to midnight (1440)
            if (endMinutes >= nightStartMinutes || startMinutes >= nightStartMinutes)
            {
                var periodStart = Math.Max(startMinutes, nightStartMinutes);
                var periodEnd = endMinutes >= nightStartMinutes ? Math.Min(endMinutes, 1440) : 1440;
                if (periodEnd > periodStart)
                    nightMinutes += periodEnd - periodStart;
            }

            // Period 2: midnight (0) to nightEnd
            if (startMinutes < nightEndMinutes || endMinutes <= nightEndMinutes)
            {
                var periodStart = startMinutes < nightEndMinutes ? startMinutes : 0;
                var periodEnd = Math.Min(endMinutes, nightEndMinutes);
                if (periodEnd > periodStart && startMinutes < nightEndMinutes)
                    nightMinutes += periodEnd - periodStart;
            }
        }
        else
        {
            // Night doesn't span midnight (e.g., 20:00 - 23:00)
            var overlapStart = Math.Max(startMinutes, nightStartMinutes);
            var overlapEnd = Math.Min(endMinutes, nightEndMinutes);
            if (overlapEnd > overlapStart)
                nightMinutes += overlapEnd - overlapStart;
        }

        return nightMinutes;
    }

    /// <summary>
    /// Calculate night hours before midnight for month-boundary shifts
    /// </summary>
    private decimal CalculateNightHoursBeforeMidnight(Shift shift, TimeOnly nightStart, TimeOnly nightEnd)
    {
        var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
        var nightStartMinutes = nightStart.Hour * 60 + nightStart.Minute;
        
        // Hours from night start to midnight
        if (startMinutes < nightStartMinutes)
        {
            // Start before night, night starts later
            return (1440 - nightStartMinutes) / 60m;
        }
        else
        {
            // Start during night
            return (1440 - startMinutes) / 60m;
        }
    }

    /// <summary>
    /// Calculate night hours after midnight from previous month shift
    /// </summary>
    private decimal CalculateNightHoursAfterMidnight(Shift shift, TimeOnly nightStart, TimeOnly nightEnd)
    {
        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
        var nightEndMinutes = nightEnd.Hour * 60 + nightEnd.Minute;
        
        // Hours from midnight to night end (or shift end, whichever is earlier)
        return Math.Min(endMinutes, nightEndMinutes) / 60m;
    }

    /// <summary>
    /// Calculate hours after midnight for spilled shifts
    /// </summary>
    private decimal CalculateHoursAfterMidnight(Shift shift)
    {
        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
        var breakProportion = shift.TotalHours > 0 
            ? endMinutes / (decimal)((24 * 60 - shift.StartTime.Hour * 60 - shift.StartTime.Minute) + endMinutes)
            : 0;
        var breakAfterMidnight = shift.BreakMinutes * breakProportion;
        
        return Math.Max(0, (endMinutes - breakAfterMidnight) / 60m);
    }

    /// <summary>
    /// Calculate shift hours for a specific month
    /// </summary>
    private decimal CalculateShiftHoursForMonth(Shift shift, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lastDayOfMonth = new DateOnly(year, month, daysInMonth);
        
        if (!shift.SpansNextDay || shift.Date != lastDayOfMonth)
            return shift.TotalHours;
        
        // Shift spans to next month
        if (shift.OvernightHoursMode == 0)
        {
            // Split at midnight
            var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
            var minutesUntilMidnight = 1440 - startMinutes;
            var totalMinutes = (int)(shift.TotalHours * 60) + shift.BreakMinutes;
            var breakProportion = totalMinutes > 0 ? (decimal)minutesUntilMidnight / totalMinutes : 0;
            var breakBeforeMidnight = shift.BreakMinutes * breakProportion;
            
            return Math.Max(0, (minutesUntilMidnight - breakBeforeMidnight) / 60m);
        }
        
        // Mode 1: All hours in current month
        return shift.TotalHours;
    }

    #endregion

    #region Attendance (Mesai Takip)

    /// <summary>
    /// Attendance tracking page - only for registered users with access
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Attendance(int? year, int? month, int? unitId)
    {
        // Only registered users can access attendance
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Attendance") });
        }
        
        // Check if user has attendance access
        var (canAccessAttendance, _) = await GetFeatureAccessAsync();
        if (!canAccessAttendance)
        {
            TempData["Error"] = "Mesai takip özelliğine erişim yetkiniz bulunmamaktadır.";
            return RedirectToAction("Index");
        }

        var selectedYear = year ?? DateTime.Now.Year;
        var selectedMonth = month ?? DateTime.Now.Month;
        
        var organization = await GetOrCreateOrganizationAsync();
        var isPremium = await IsPremiumUserAsync();
        
        // Load units for premium users
        var units = new List<Unit>();
        if (isPremium)
        {
            try
            {
                // Initialize defaults if needed (same as Index)
                await InitializeDefaultUnitTypesAsync(organization.Id);
                await InitializeDefaultUnitAsync(organization.Id);
                
                units = await _context.Units
                    .Where(u => u.OrganizationId == organization.Id && u.IsActive)
                    .OrderBy(u => u.SortOrder)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load units for Attendance page");
            }
        }
        
        // Build employee query with optional unit filter
        var employeeQuery = _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive);
            
        if (isPremium && unitId.HasValue)
        {
            employeeQuery = employeeQuery.Where(e => e.UnitId == unitId.Value);
        }
        
        var employees = await employeeQuery
            .OrderBy(e => e.FullName)
            .ToListAsync();
        
        var employeeIds = employees.Select(e => e.Id).ToList();
            
        var attendances = await _context.TimeAttendances
            .Where(a => employeeIds.Contains(a.EmployeeId))
            .Where(a => a.Date.Year == selectedYear && a.Date.Month == selectedMonth)
            .ToListAsync();
            
        var shifts = await _context.Shifts
            .Where(s => employeeIds.Contains(s.EmployeeId))
            .Where(s => s.Date.Year == selectedYear && s.Date.Month == selectedMonth)
            .ToListAsync();
            
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id)
            .Where(h => h.Date.Year == selectedYear && h.Date.Month == selectedMonth)
            .ToListAsync();

        // Get shift templates for quick apply
        var shiftTemplates = await _context.ShiftTemplates
            .Where(t => t.OrganizationId == organization.Id && t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();

        var viewModel = new AttendanceViewModel
        {
            Organization = organization,
            Employees = employees,
            Attendances = attendances,
            Shifts = shifts,
            Holidays = holidays,
            Units = units,
            SelectedUnitId = unitId,
            IsPremium = isPremium,
            SelectedYear = selectedYear,
            SelectedMonth = selectedMonth
        };
        
        ViewBag.ShiftTemplates = shiftTemplates;

        return View(viewModel);
    }

    /// <summary>
    /// Add or update attendance record (manual entry)
    /// </summary>
    [HttpPost("api/attendance/manual")]
    public async Task<IActionResult> SaveAttendance([FromBody] ManualAttendanceDto dto)
    {
        try
        {
            _logger.LogInformation("SaveAttendance called: EmployeeId={EmpId}, Date={Date}", dto?.EmployeeId, dto?.Date);
            
            if (User.Identity?.IsAuthenticated != true)
            {
                _logger.LogWarning("SaveAttendance: User not authenticated");
                return Unauthorized(new { success = false, error = "Not authenticated" });
            }

            if (dto == null)
            {
                _logger.LogWarning("SaveAttendance: dto is null");
                return BadRequest(new { success = false, error = "Request body is null" });
            }

            var organization = await GetOrCreateOrganizationAsync();
            
            if (organization == null)
            {
                _logger.LogWarning("SaveAttendance: Organization is null for user");
                return BadRequest(new { success = false, error = "Organization not found" });
            }
            
            _logger.LogInformation("SaveAttendance: OrgId={OrgId}", organization.Id);
            
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.OrganizationId == organization.Id);
                
            if (employee == null)
            {
                _logger.LogWarning("SaveAttendance: Employee not found. EmpId={EmpId}, OrgId={OrgId}", dto.EmployeeId, organization.Id);
                return NotFound(new { success = false, error = "Personel bulunamadı" });
            }

            var date = DateOnly.Parse(dto.Date);
            _logger.LogInformation("SaveAttendance: Parsed date={Date}", date);
            
            var attendance = await _context.TimeAttendances
                .FirstOrDefaultAsync(a => a.EmployeeId == dto.EmployeeId && a.Date == date);

            if (attendance == null)
            {
                _logger.LogInformation("SaveAttendance: Creating new attendance record");
                attendance = new TimeAttendance
                {
                    EmployeeId = dto.EmployeeId,
                    Date = date,
                    Source = AttendanceSource.Manual
                };
                _context.TimeAttendances.Add(attendance);
            }
            else
            {
                _logger.LogInformation("SaveAttendance: Updating existing attendance record Id={Id}", attendance.Id);
            }

            // Allow setting only check-in, only check-out, or both
            if (!string.IsNullOrEmpty(dto.CheckInTime))
                attendance.CheckInTime = TimeOnly.Parse(dto.CheckInTime);
            else if (dto.ClearCheckIn)
                attendance.CheckInTime = null;
                
            if (!string.IsNullOrEmpty(dto.CheckOutTime))
                attendance.CheckOutTime = TimeOnly.Parse(dto.CheckOutTime);
            else if (dto.ClearCheckOut)
                attendance.CheckOutTime = null;
                
            attendance.CheckOutToNextDay = dto.CheckOutToNextDay;
            attendance.Notes = dto.Notes;
            attendance.Type = dto.Type;
            attendance.UpdatedAt = DateTime.UtcNow;

            // Calculate worked hours only if both times are present
            if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
            {
                var inMinutes = attendance.CheckInTime.Value.Hour * 60 + attendance.CheckInTime.Value.Minute;
                var outMinutes = attendance.CheckOutTime.Value.Hour * 60 + attendance.CheckOutTime.Value.Minute;
                
                // Handle overnight shifts (check-out next day)
                if (attendance.CheckOutToNextDay || outMinutes < inMinutes)
                    outMinutes += 24 * 60;
                
                var totalMinutes = outMinutes - inMinutes;
                
                // Check if there's a shift for this day and subtract break time
                var shift = await _context.Shifts
                    .Include(s => s.ShiftTemplate)
                    .FirstOrDefaultAsync(s => s.EmployeeId == dto.EmployeeId && s.Date == date);
                
                int breakMinutes = 0;
                if (shift != null)
                {
                    // Use shift's break minutes if set, otherwise use template's break minutes
                    // Do NOT fall back to organization default - only use what's defined in shift/template
                    if (shift.BreakMinutes > 0)
                        breakMinutes = shift.BreakMinutes;
                    else if (shift.ShiftTemplate?.BreakMinutes.HasValue == true)
                        breakMinutes = shift.ShiftTemplate.BreakMinutes.Value;
                    // else breakMinutes stays 0 (no break defined for this shift)
                    _logger.LogInformation("SaveAttendance: Found shift with break={Break}min", breakMinutes);
                }
                
                totalMinutes -= breakMinutes;
                if (totalMinutes < 0) totalMinutes = 0;
                    
                attendance.WorkedHours = Math.Round(totalMinutes / 60m, 2);
            }
            else
            {
                attendance.WorkedHours = null; // Clear if not both times present
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("SaveAttendance: Success, Id={Id}", attendance.Id);

            return Ok(new { 
                success = true, 
                message = "Mesai kaydı güncellendi",
                data = new {
                    id = attendance.Id,
                    checkIn = attendance.CheckInTime?.ToString("HH:mm"),
                    checkOut = attendance.CheckOutTime?.ToString("HH:mm"),
                    workedHours = attendance.WorkedHours
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveAttendance error: {Message}", ex.Message);
            return StatusCode(500, new { 
                success = false, 
                error = ex.Message, 
                innerError = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Delete attendance record
    /// </summary>
    [HttpDelete("api/attendance/{id}")]
    public async Task<IActionResult> DeleteAttendance(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var organization = await GetOrCreateOrganizationAsync();
        
        var attendance = await _context.TimeAttendances
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Id == id && a.Employee.OrganizationId == organization.Id);
            
        if (attendance == null)
            return NotFound();

        _context.TimeAttendances.Remove(attendance);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Kayıt silindi" });
    }

    /// <summary>
    /// Get attendance data for a specific month (AJAX)
    /// </summary>
    [HttpGet("api/attendance/month")]
    public async Task<IActionResult> GetMonthAttendance(int year, int month)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var organization = await GetOrCreateOrganizationAsync();
        
        var attendances = await _context.TimeAttendances
            .Include(a => a.Employee)
            .Where(a => a.Employee.OrganizationId == organization.Id)
            .Where(a => a.Date.Year == year && a.Date.Month == month)
            .Select(a => new {
                id = a.Id,
                employeeId = a.EmployeeId,
                date = a.Date.ToString("yyyy-MM-dd"),
                checkIn = a.CheckInTime.HasValue ? a.CheckInTime.Value.ToString("HH:mm") : null,
                checkOut = a.CheckOutTime.HasValue ? a.CheckOutTime.Value.ToString("HH:mm") : null,
                checkOutToNextDay = a.CheckOutToNextDay,
                workedHours = a.WorkedHours,
                type = a.Type.ToString(),
                source = a.Source.ToString(),
                notes = a.Notes
            })
            .ToListAsync();

        return Ok(new { success = true, data = attendances });
    }

    #endregion

    #region Payroll API

    /// <summary>
    /// Save payroll calculation
    /// </summary>
    [HttpPost("api/payroll/save")]
    public async Task<IActionResult> SavePayroll([FromBody] SavePayrollDto dto)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var organization = await GetOrCreateOrganizationAsync();
        if (organization == null)
            return BadRequest(new { success = false, error = "Organization not found" });

        var savedPayroll = new SavedPayroll
        {
            OrganizationId = organization.Id,
            Name = dto.Name,
            Year = dto.Year,
            Month = dto.Month,
            DataSource = dto.DataSource,
            NightStartHour = dto.NightStartHour,
            NightEndHour = dto.NightEndHour,
            PayrollDataJson = dto.PayrollDataJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SavedPayrolls.Add(savedPayroll);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, id = savedPayroll.Id, message = "Puantaj kaydedildi" });
    }

    /// <summary>
    /// Update saved payroll
    /// </summary>
    [HttpPut("api/payroll/{id}")]
    public async Task<IActionResult> UpdatePayroll(int id, [FromBody] SavePayrollDto dto)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var organization = await GetOrCreateOrganizationAsync();
        
        var savedPayroll = await _context.SavedPayrolls
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organization.Id);
            
        if (savedPayroll == null)
            return NotFound();

        savedPayroll.Name = dto.Name;
        savedPayroll.PayrollDataJson = dto.PayrollDataJson;
        savedPayroll.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Puantaj güncellendi" });
    }

    /// <summary>
    /// Delete saved payroll
    /// </summary>
    [HttpDelete("api/payroll/{id}")]
    public async Task<IActionResult> DeletePayroll(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var organization = await GetOrCreateOrganizationAsync();
        
        var savedPayroll = await _context.SavedPayrolls
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organization.Id);
            
        if (savedPayroll == null)
            return NotFound();

        _context.SavedPayrolls.Remove(savedPayroll);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Puantaj silindi" });
    }

    /// <summary>
    /// Get saved payroll
    /// </summary>
    [HttpGet("api/payroll/{id}")]
    public async Task<IActionResult> GetPayroll(int id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var organization = await GetOrCreateOrganizationAsync();
        
        var savedPayroll = await _context.SavedPayrolls
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organization.Id);
            
        if (savedPayroll == null)
            return NotFound();

        return Ok(new { 
            success = true, 
            data = new {
                id = savedPayroll.Id,
                name = savedPayroll.Name,
                year = savedPayroll.Year,
                month = savedPayroll.Month,
                dataSource = savedPayroll.DataSource,
                nightStartHour = savedPayroll.NightStartHour,
                nightEndHour = savedPayroll.NightEndHour,
                payrollData = savedPayroll.PayrollDataJson,
                createdAt = savedPayroll.CreatedAt
            }
        });
    }

    public class SavePayrollDto
    {
        public string Name { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public string DataSource { get; set; } = "shift";
        public int NightStartHour { get; set; } = 22;
        public int NightEndHour { get; set; } = 6;
        public string PayrollDataJson { get; set; } = "[]";
    }

    #endregion

    #region Helper Methods

    private async Task<Organization> GetOrCreateOrganizationAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var org = await _context.Organizations
                    .FirstOrDefaultAsync(o => o.UserId == user.Id);
                    
                if (org == null)
                {
                    org = new Organization
                    {
                        UserId = user.Id,
                        Name = user.FullName ?? "My Organization"
                    };
                    
                    _context.Organizations.Add(org);
                    await _context.SaveChangesAsync();
                }
                
                // Ensure organization has default templates (for existing users too)
                await CopyDefaultTemplatesToOrganization(org.Id);
                
                return org;
            }
        }
        
        // Guest user - use session
        var sessionId = HttpContext.Session.GetString("GuestSessionId");
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
            HttpContext.Session.SetString("GuestSessionId", sessionId);
        }
        
        var guestOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.GuestSessionId == sessionId);
            
        if (guestOrg == null)
        {
            guestOrg = new Organization
            {
                GuestSessionId = sessionId,
                Name = "Guest Organization"
            };
            
            _context.Organizations.Add(guestOrg);
            await _context.SaveChangesAsync();
        }
        
        // Ensure organization has default templates
        await CopyDefaultTemplatesToOrganization(guestOrg.Id);
        
        return guestOrg;
    }
    
    private async Task CopyDefaultTemplatesToOrganization(int organizationId)
    {
        // Check if default templates have already been initialized for this organization
        var org = await _context.Organizations.FindAsync(organizationId);
        if (org == null || org.DefaultTemplatesInitialized) return;
        
        // Mark as initialized so we don't add again even if user deletes all templates
        org.DefaultTemplatesInitialized = true;
        await _context.SaveChangesAsync();
        
        // Default templates to copy
        var defaultTemplates = new List<ShiftTemplate>
        {
            new ShiftTemplate
            {
                OrganizationId = organizationId,
                Name = "08:00 - 17:00",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 0),
                SpansNextDay = false,
                BreakMinutes = 60,
                Color = "#22C55E",
                IsGlobal = false,
                DisplayOrder = 1,
                IsActive = true
            },
            new ShiftTemplate
            {
                OrganizationId = organizationId,
                Name = "08:00 - 18:00",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(18, 0),
                SpansNextDay = false,
                BreakMinutes = 60,
                Color = "#3B82F6",
                IsGlobal = false,
                DisplayOrder = 2,
                IsActive = true
            },
            new ShiftTemplate
            {
                OrganizationId = organizationId,
                Name = "16:00 - 08:00",
                StartTime = new TimeOnly(16, 0),
                EndTime = new TimeOnly(8, 0),
                SpansNextDay = true,
                BreakMinutes = 0,
                Color = "#EF4444",
                IsGlobal = false,
                DisplayOrder = 3,
                IsActive = true
            },
            new ShiftTemplate
            {
                OrganizationId = organizationId,
                Name = "08:00 - 08:00 (24s)",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(8, 0),
                SpansNextDay = true,
                BreakMinutes = 0,
                Color = "#8B5CF6",
                IsGlobal = false,
                DisplayOrder = 4,
                IsActive = true
            }
        };
        
        _context.ShiftTemplates.AddRange(defaultTemplates);
        await _context.SaveChangesAsync();
    }

    private async Task<int> GetEmployeeLimitAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var defaultLimit = await _settingsService.GetRegisteredEmployeeLimitAsync();
                var premiumLimit = await _settingsService.GetPremiumEmployeeLimitAsync();
                return user.GetEffectiveEmployeeLimit(defaultLimit, premiumLimit);
            }
        }
        
        return await _settingsService.GetGuestEmployeeLimitAsync();
    }
    
    private async Task<(bool CanAccessAttendance, bool CanAccessPayroll)> GetFeatureAccessAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            // Unregistered users cannot access attendance or payroll
            return (false, false);
        }
        
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            return (user.CanAccessAttendance, user.CanAccessPayroll);
        }
        
        // Default for registered users
        return (true, true);
    }

    private void CalculateShiftHours(Shift shift, Organization org)
    {
        // Calculate total hours
        var start = shift.StartTime;
        var end = shift.EndTime;
        
        decimal totalMinutes;
        if (shift.SpansNextDay)
        {
            // Shift spans to next day
            totalMinutes = (24 * 60 - start.Hour * 60 - start.Minute) + (end.Hour * 60 + end.Minute);
        }
        else
        {
            totalMinutes = (end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute);
        }
        
        shift.TotalHours = (totalMinutes - shift.BreakMinutes) / 60m;
        
        // Calculate night hours
        var nightStart = org.NightStartTime;
        var nightEnd = org.NightEndTime;
        
        shift.NightHours = CalculateNightHours(start, end, shift.SpansNextDay, nightStart, nightEnd);
        
        // Check if weekend
        var dayOfWeek = shift.Date.DayOfWeek;
        var weekendDays = org.WeekendDays.Split(',').Select(int.Parse).ToList();
        shift.IsWeekend = weekendDays.Contains((int)dayOfWeek);
    }

    private decimal CalculateNightHours(TimeOnly start, TimeOnly end, bool spansNextDay, TimeOnly nightStart, TimeOnly nightEnd)
    {
        // Simplified night hours calculation
        // Night is typically 20:00 - 06:00
        decimal nightMinutes = 0;
        
        if (spansNextDay)
        {
            // From start to midnight
            if (start < nightStart)
            {
                // Add hours from nightStart to midnight
                nightMinutes += (24 * 60) - (nightStart.Hour * 60 + nightStart.Minute);
            }
            else
            {
                // Add hours from start to midnight
                nightMinutes += (24 * 60) - (start.Hour * 60 + start.Minute);
            }
            
            // From midnight to end (or nightEnd)
            var effectiveEnd = end < nightEnd ? end : nightEnd;
            nightMinutes += effectiveEnd.Hour * 60 + effectiveEnd.Minute;
        }
        else
        {
            // Same day
            if (start >= nightStart || end <= nightEnd)
            {
                var effectiveStart = start > nightStart ? start : nightStart;
                var effectiveEnd = end < nightEnd ? end : nightEnd;
                
                if (effectiveEnd > effectiveStart)
                {
                    nightMinutes = (effectiveEnd.Hour * 60 + effectiveEnd.Minute) - 
                                   (effectiveStart.Hour * 60 + effectiveStart.Minute);
                }
            }
        }
        
        return nightMinutes / 60m;
    }

    private static string GetRandomColor()
    {
        var colors = new[] { "#3B82F6", "#EF4444", "#22C55E", "#F59E0B", "#8B5CF6", "#EC4899", "#06B6D4", "#84CC16" };
        return colors[Random.Shared.Next(colors.Length)];
    }

    #endregion
    
    #region Unit Management (Premium Feature)
    
    /// <summary>
    /// Check if current user has premium access (Premium plan OR CanManageUnits permission)
    /// </summary>
    private async Task<bool> IsPremiumUserAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return false;
            
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return false;
        
        // Check CanManageUnits permission (admin-granted)
        if (user.CanManageUnits)
            return true;
            
        // Check if premium and not expired
        if (user.Plan == UserPlan.Premium)
        {
            if (user.PremiumExpiresAt == null || user.PremiumExpiresAt > DateTime.UtcNow)
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Initialize default unit types for organization (Premium feature)
    /// </summary>
    private async Task InitializeDefaultUnitTypesAsync(int organizationId)
    {
        var existingTypes = await _context.UnitTypes
            .AnyAsync(ut => ut.OrganizationId == organizationId);
            
        if (existingTypes)
            return;
            
        var defaultTypes = new[]
        {
            new UnitType { OrganizationId = organizationId, Name = "Poliklinik/Servis", NameEn = "Polyclinic/Service", DefaultCoefficient = 1.0m, Color = "#3B82F6", Icon = "hospital", SortOrder = 1, IsSystem = true },
            new UnitType { OrganizationId = organizationId, Name = "Yoğun Bakım", NameEn = "Intensive Care Unit", DefaultCoefficient = 1.5m, Color = "#EF4444", Icon = "heart-pulse", SortOrder = 2, IsSystem = true },
            new UnitType { OrganizationId = organizationId, Name = "Radyasyon Birimi", NameEn = "Radiation Unit", DefaultCoefficient = 1.5m, Color = "#F59E0B", Icon = "radiation", SortOrder = 3, IsSystem = true }
        };
        
        _context.UnitTypes.AddRange(defaultTypes);
        await _context.SaveChangesAsync();
    }
    
    /// <summary>
    /// Initialize default unit for organization (Premium feature)
    /// Creates "Genel Birim" if no units exist and assigns all employees to it
    /// </summary>
    private async Task InitializeDefaultUnitAsync(int organizationId)
    {
        var existingUnits = await _context.Units
            .AnyAsync(u => u.OrganizationId == organizationId);
            
        if (existingUnits)
            return;
            
        // Get first unit type (Poliklinik/Servis)
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
            SortOrder = 1
        };
        
        _context.Units.Add(defaultUnit);
        await _context.SaveChangesAsync();
        
        // Assign all existing employees to this default unit
        var employeesWithoutUnit = await _context.Employees
            .Where(e => e.OrganizationId == organizationId && e.UnitId == null && e.IsActive)
            .ToListAsync();
            
        foreach (var emp in employeesWithoutUnit)
        {
            emp.UnitId = defaultUnit.Id;
        }
        
        if (employeesWithoutUnit.Any())
        {
            await _context.SaveChangesAsync();
        }
    }
    
    // GET: /api/unit-types
    [HttpGet]
    [Route("api/unit-types")]
    public async Task<IActionResult> GetUnitTypes()
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        // Initialize default types if needed
        await InitializeDefaultUnitTypesAsync(organization.Id);
        
        var unitTypes = await _context.UnitTypes
            .Where(ut => ut.OrganizationId == organization.Id && ut.IsActive)
            .OrderBy(ut => ut.SortOrder)
            .Select(ut => new {
                ut.Id,
                ut.Name,
                ut.NameEn,
                ut.Description,
                ut.DefaultCoefficient,
                ut.Color,
                ut.Icon,
                ut.SortOrder,
                ut.IsSystem,
                UnitCount = ut.Units.Count(u => u.IsActive)
            })
            .ToListAsync();
            
        return Json(unitTypes);
    }
    
    // POST: /api/unit-types
    [HttpPost]
    [Route("api/unit-types")]
    public async Task<IActionResult> CreateUnitType([FromBody] UnitTypeDto dto)
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        // Check for duplicate name
        var exists = await _context.UnitTypes
            .AnyAsync(ut => ut.OrganizationId == organization.Id && ut.Name == dto.Name);
        if (exists)
            return BadRequest(new { error = "Bu isimde bir birim tipi zaten mevcut" });
        
        var maxOrder = await _context.UnitTypes
            .Where(ut => ut.OrganizationId == organization.Id)
            .MaxAsync(ut => (int?)ut.SortOrder) ?? 0;
        
        var unitType = new UnitType
        {
            OrganizationId = organization.Id,
            Name = dto.Name,
            Description = dto.Description,
            DefaultCoefficient = dto.DefaultCoefficient > 0 ? dto.DefaultCoefficient : 1.0m,
            Color = dto.Color ?? GetRandomColor(),
            Icon = dto.Icon,
            SortOrder = maxOrder + 1,
            IsSystem = false
        };
        
        _context.UnitTypes.Add(unitType);
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.UnitTypeCreated, 
            $"Birim tipi eklendi: {unitType.Name}", 
            "UnitType", unitType.Id,
            new { unitType.Name, unitType.Description });
        
        return Json(new { 
            unitType.Id, 
            unitType.Name, 
            unitType.Description,
            unitType.DefaultCoefficient,
            unitType.Color,
            unitType.Icon,
            unitType.SortOrder,
            unitType.IsSystem,
            UnitCount = 0
        });
    }
    
    // PUT: /api/unit-types/{id}
    [HttpPut]
    [Route("api/unit-types/{id}")]
    public async Task<IActionResult> UpdateUnitType(int id, [FromBody] UnitTypeDto dto)
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        var unitType = await _context.UnitTypes
            .FirstOrDefaultAsync(ut => ut.Id == id && ut.OrganizationId == organization.Id);
            
        if (unitType == null)
            return NotFound(new { error = "Birim tipi bulunamadı" });
        
        // Check for duplicate name (excluding current)
        var exists = await _context.UnitTypes
            .AnyAsync(ut => ut.OrganizationId == organization.Id && ut.Name == dto.Name && ut.Id != id);
        if (exists)
            return BadRequest(new { error = "Bu isimde bir birim tipi zaten mevcut" });
        
        unitType.Name = dto.Name;
        unitType.Description = dto.Description;
        unitType.DefaultCoefficient = dto.DefaultCoefficient > 0 ? dto.DefaultCoefficient : 1.0m;
        if (!string.IsNullOrEmpty(dto.Color))
            unitType.Color = dto.Color;
        unitType.Icon = dto.Icon;
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.UnitTypeUpdated, 
            $"Birim tipi güncellendi: {unitType.Name}", 
            "UnitType", unitType.Id,
            new { unitType.Name, unitType.Description });
        
        return Json(new { success = true });
    }
    
    // DELETE: /api/unit-types/{id}
    [HttpDelete]
    [Route("api/unit-types/{id}")]
    public async Task<IActionResult> DeleteUnitType(int id)
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        var unitType = await _context.UnitTypes
            .Include(ut => ut.Units)
            .FirstOrDefaultAsync(ut => ut.Id == id && ut.OrganizationId == organization.Id);
            
        if (unitType == null)
            return NotFound(new { error = "Birim tipi bulunamadı" });
            
        if (unitType.IsSystem)
            return BadRequest(new { error = "Sistem birim tipleri silinemez" });
            
        // Check if any units are using this type
        if (unitType.Units.Any(u => u.IsActive))
            return BadRequest(new { error = "Bu birim tipine atanmış birimler var. Önce birimlerin tiplerini değiştirin." });
        
        var typeName = unitType.Name;
        unitType.IsActive = false;
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.UnitTypeDeleted, 
            $"Birim tipi silindi: {typeName}", 
            "UnitType", id,
            new { Name = typeName });
        
        return Json(new { success = true });
    }
    
    // GET: /api/units
    [HttpGet]
    [Route("api/units")]
    public async Task<IActionResult> GetUnits()
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        var units = await _context.Units
            .Include(u => u.UnitType)
            .Where(u => u.OrganizationId == organization.Id && u.IsActive)
            .OrderBy(u => u.SortOrder)
            .ThenBy(u => u.Name)
            .Select(u => new {
                u.Id,
                u.Name,
                u.Description,
                u.UnitTypeId,
                UnitTypeName = u.UnitType != null ? u.UnitType.Name : null,
                UnitTypeColor = u.UnitType != null ? u.UnitType.Color : null,
                u.Coefficient,
                u.Color,
                u.IsDefault,
                u.SortOrder,
                EmployeeCount = u.Employees.Count(e => e.IsActive)
            })
            .ToListAsync();
            
        return Json(units);
    }
    
    // POST: /api/units
    [HttpPost]
    [Route("api/units")]
    public async Task<IActionResult> CreateUnit([FromBody] UnitDto dto)
    {
        try
        {
            if (!await IsPremiumUserAsync())
                return Unauthorized(new { error = "Premium üyelik gerekli" });
                
            var organization = await GetOrCreateOrganizationAsync();
            
            // Check unit limit
            var user = await _userManager.GetUserAsync(User);
            if (user != null && user.UnitLimit > 0)
            {
                var currentUnitCount = await _context.Units
                    .CountAsync(u => u.OrganizationId == organization.Id && u.IsActive);
                if (currentUnitCount >= user.UnitLimit)
                    return BadRequest(new { error = $"Birim limitine ulaşıldı ({user.UnitLimit} birim). Daha fazla birim için yöneticiyle iletişime geçin." });
            }
            
            // Check for duplicate name
            var exists = await _context.Units
                .AnyAsync(u => u.OrganizationId == organization.Id && u.Name == dto.Name && u.IsActive);
            if (exists)
                return BadRequest(new { error = "Bu isimde bir birim zaten mevcut" });
            
            // Validate unit type if provided
            if (dto.UnitTypeId.HasValue)
            {
                var typeExists = await _context.UnitTypes
                    .AnyAsync(ut => ut.Id == dto.UnitTypeId && ut.OrganizationId == organization.Id);
                if (!typeExists)
                    return BadRequest(new { error = "Geçersiz birim tipi" });
            }
            
            var maxOrder = await _context.Units
                .Where(u => u.OrganizationId == organization.Id)
                .MaxAsync(u => (int?)u.SortOrder) ?? 0;
            
            // Get default coefficient from unit type if not specified
            decimal coefficient = dto.Coefficient;
            if (coefficient <= 0 && dto.UnitTypeId.HasValue)
            {
                var unitType = await _context.UnitTypes.FindAsync(dto.UnitTypeId.Value);
                coefficient = unitType?.DefaultCoefficient ?? 1.0m;
            }
            if (coefficient <= 0) coefficient = 1.0m;
            
            var unit = new Unit
            {
                OrganizationId = organization.Id,
                UnitTypeId = dto.UnitTypeId,
                Name = dto.Name,
                Description = dto.Description,
                Coefficient = coefficient,
                Color = dto.Color ?? GetRandomColor(),
                IsDefault = dto.IsDefault,
                EmployeeLimit = dto.EmployeeLimit,
                SortOrder = maxOrder + 1
            };
            
            // If this is marked as default, unset other defaults
            if (dto.IsDefault)
            {
                await _context.Units
                    .Where(u => u.OrganizationId == organization.Id && u.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsDefault, false));
            }
            
            _context.Units.Add(unit);
            await _context.SaveChangesAsync();
            
            await _activityLog.LogAsync(ActivityType.UnitCreated, 
                $"Birim eklendi: {unit.Name}", 
                "Unit", unit.Id,
                new { unit.Name, unit.Description, unit.Coefficient });
            
            return Json(new { 
                unit.Id, 
                unit.Name, 
                unit.Description,
                unit.UnitTypeId,
                unit.Coefficient,
                unit.Color,
                unit.IsDefault,
                unit.SortOrder,
                EmployeeCount = 0
            });
        }
        catch (Exception ex)
        {
            // Get the deepest inner exception
            var innerEx = ex;
            while (innerEx.InnerException != null)
                innerEx = innerEx.InnerException;
            
            var errorMessage = innerEx.Message;
            _logger.LogError(ex, "Error creating unit: {ErrorMessage}", errorMessage);
            return StatusCode(500, new { error = "Birim oluşturulurken bir hata oluştu: " + errorMessage });
        }
    }
    
    // PUT: /api/units/{id}
    [HttpPut]
    [Route("api/units/{id}")]
    public async Task<IActionResult> UpdateUnit(int id, [FromBody] UnitDto dto)
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        var unit = await _context.Units
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == organization.Id);
            
        if (unit == null)
            return NotFound(new { error = "Birim bulunamadı" });
        
        // Check for duplicate name (excluding current)
        var exists = await _context.Units
            .AnyAsync(u => u.OrganizationId == organization.Id && u.Name == dto.Name && u.Id != id && u.IsActive);
        if (exists)
            return BadRequest(new { error = "Bu isimde bir birim zaten mevcut" });
        
        // Validate unit type if provided
        if (dto.UnitTypeId.HasValue)
        {
            var typeExists = await _context.UnitTypes
                .AnyAsync(ut => ut.Id == dto.UnitTypeId && ut.OrganizationId == organization.Id);
            if (!typeExists)
                return BadRequest(new { error = "Geçersiz birim tipi" });
        }
        
        unit.Name = dto.Name;
        unit.Description = dto.Description;
        unit.UnitTypeId = dto.UnitTypeId;
        unit.Coefficient = dto.Coefficient > 0 ? dto.Coefficient : 1.0m;
        unit.EmployeeLimit = dto.EmployeeLimit;
        if (!string.IsNullOrEmpty(dto.Color))
            unit.Color = dto.Color;
        unit.UpdatedAt = DateTime.UtcNow;
        
        // Handle default setting
        if (dto.IsDefault && !unit.IsDefault)
        {
            await _context.Units
                .Where(u => u.OrganizationId == organization.Id && u.IsDefault && u.Id != id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsDefault, false));
            unit.IsDefault = true;
        }
        else if (!dto.IsDefault)
        {
            unit.IsDefault = false;
        }
        
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.UnitUpdated, 
            $"Birim güncellendi: {unit.Name}", 
            "Unit", unit.Id,
            new { unit.Name, unit.Description, unit.Coefficient });
        
        return Json(new { success = true });
    }
    
    // DELETE: /api/units/{id}
    [HttpDelete]
    [Route("api/units/{id}")]
    public async Task<IActionResult> DeleteUnit(int id)
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        var unit = await _context.Units
            .Include(u => u.Employees.Where(e => e.IsActive))
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == organization.Id);
            
        if (unit == null)
            return NotFound(new { error = "Birim bulunamadı" });
            
        // Check if any employees are in this unit
        if (unit.Employees.Any())
            return BadRequest(new { error = "Bu birimde personel bulunmakta. Önce personelleri başka birime taşıyın." });
        
        var unitName = unit.Name;
        unit.IsActive = false;
        unit.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        await _activityLog.LogAsync(ActivityType.UnitDeleted, 
            $"Birim silindi: {unitName}", 
            "Unit", id,
            new { Name = unitName });
        
        return Json(new { success = true });
    }
    
    // POST: /api/units/{id}/employees - Assign employees to unit
    [HttpPost]
    [Route("api/units/{id}/employees")]
    public async Task<IActionResult> AssignEmployeesToUnit(int id, [FromBody] AssignEmployeesDto dto)
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        var unit = await _context.Units
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == organization.Id && u.IsActive);
            
        if (unit == null)
            return NotFound(new { error = "Birim bulunamadı" });
        
        var currentCount = await _context.Employees
            .CountAsync(e => e.UnitId == id && e.IsActive);
        
        // Check user's unit employee limit
        var user = await _userManager.GetUserAsync(User);
        if (user != null && user.UnitEmployeeLimit > 0)
        {
            if (currentCount + dto.EmployeeIds.Count > user.UnitEmployeeLimit)
                return BadRequest(new { error = $"Birime personel ekleme limiti aşıldı (Limit: {user.UnitEmployeeLimit}, Mevcut: {currentCount})" });
        }
        
        // Check unit-specific employee limit (if set by admin in unit settings)
        if (unit.EmployeeLimit > 0)
        {
            if (currentCount + dto.EmployeeIds.Count > unit.EmployeeLimit)
                return BadRequest(new { error = $"Birim personel limiti aşıldı (Limit: {unit.EmployeeLimit}, Mevcut: {currentCount})" });
        }
        
        // Update employees
        var employees = await _context.Employees
            .Where(e => dto.EmployeeIds.Contains(e.Id) && e.OrganizationId == organization.Id)
            .ToListAsync();
            
        foreach (var emp in employees)
        {
            emp.UnitId = id;
            emp.UpdatedAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        
        return Json(new { success = true, count = employees.Count });
    }
    
    // DELETE: /api/units/{id}/employees/{employeeId} - Remove employee from unit
    [HttpDelete]
    [Route("api/units/{id}/employees/{employeeId}")]
    public async Task<IActionResult> RemoveEmployeeFromUnit(int id, int employeeId)
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.OrganizationId == organization.Id && e.UnitId == id);
            
        if (employee == null)
            return NotFound(new { error = "Personel bulunamadı" });
        
        employee.UnitId = null;
        employee.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return Json(new { success = true });
    }
    
    // GET: /api/units/{id}/employees - Get employees in a unit
    [HttpGet]
    [Route("api/units/{id}/employees")]
    public async Task<IActionResult> GetUnitEmployees(int id)
    {
        if (!await IsPremiumUserAsync())
            return Unauthorized(new { error = "Premium üyelik gerekli" });
            
        var organization = await GetOrCreateOrganizationAsync();
        
        var unit = await _context.Units
            .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == organization.Id);
            
        if (unit == null)
            return NotFound(new { error = "Birim bulunamadı" });
        
        var employees = await _context.Employees
            .Where(e => e.UnitId == id && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new {
                e.Id,
                e.FullName,
                e.Title,
                e.Color
            })
            .ToListAsync();
            
        return Json(employees);
    }
    
    #endregion
    
    #region API Credentials Management (Mesai API)
    
    /// <summary>
    /// Get current API credentials info
    /// </summary>
    [HttpGet]
    [Route("api/credentials")]
    public async Task<IActionResult> GetApiCredentials()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Giriş yapmanız gerekiyor" });
            
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        
        var credential = await _context.UserApiCredentials
            .FirstOrDefaultAsync(c => c.UserId == user!.Id && c.OrganizationId == organization.Id);
        
        // Calculate monthly limit based on user's employee limit (not current count)
        var employeeLimit = user!.GetEffectiveEmployeeLimit();
        var daysInMonth = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
        var monthlyLimit = employeeLimit * daysInMonth * 2; // employee limit x days x 2
        
        if (credential == null)
        {
            return Json(new {
                hasCredentials = false,
                monthlyLimit = monthlyLimit,
                employeeLimit = employeeLimit
            });
        }
        
        // Reset counter if needed
        if (DateTime.UtcNow >= credential.MonthlyResetDate)
        {
            credential.CurrentMonthRequests = 0;
            credential.MonthlyResetDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
            credential.MonthlyRequestLimit = monthlyLimit;
            await _context.SaveChangesAsync();
        }
        
        return Json(new {
            hasCredentials = true,
            username = credential.ApiUsername,
            isActive = credential.IsActive,
            monthlyLimit = credential.MonthlyRequestLimit,
            usedThisMonth = credential.CurrentMonthRequests,
            remainingThisMonth = Math.Max(0, credential.MonthlyRequestLimit - credential.CurrentMonthRequests),
            resetDate = credential.MonthlyResetDate.ToString("yyyy-MM-dd"),
            totalRequestsAllTime = credential.TotalRequests,
            lastUsed = credential.LastUsedAt?.ToString("yyyy-MM-dd HH:mm"),
            employeeLimit = employeeLimit
        });
    }
    
    /// <summary>
    /// Create or update API credentials
    /// </summary>
    [HttpPost]
    [Route("api/credentials")]
    public async Task<IActionResult> CreateOrUpdateApiCredentials([FromBody] ApiCredentialDto dto)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Giriş yapmanız gerekiyor" });
            
        if (string.IsNullOrWhiteSpace(dto.Username) || dto.Username.Length < 3)
            return BadRequest(new { error = "Kullanıcı adı en az 3 karakter olmalı" });
            
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest(new { error = "Şifre en az 6 karakter olmalı" });
        
        // Check username availability
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        
        var usernameExists = await _context.UserApiCredentials
            .AnyAsync(c => c.ApiUsername == dto.Username && c.UserId != user!.Id);
        if (usernameExists)
            return BadRequest(new { error = "Bu kullanıcı adı zaten kullanılıyor" });
        
        var credential = await _context.UserApiCredentials
            .FirstOrDefaultAsync(c => c.UserId == user!.Id && c.OrganizationId == organization.Id);
        
        // Calculate monthly limit based on user's employee limit
        var employeeLimit = user!.GetEffectiveEmployeeLimit();
        var daysInMonth = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
        var monthlyLimit = employeeLimit * daysInMonth * 2;
        
        try
        {
            if (credential == null)
            {
                credential = new UserApiCredential
                {
                    UserId = user!.Id,
                    OrganizationId = organization.Id,
                    ApiUsername = dto.Username,
                    ApiPasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    MonthlyRequestLimit = monthlyLimit,
                    MonthlyResetDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1),
                    IsActive = true
                };
                _context.UserApiCredentials.Add(credential);
            }
            else
            {
                credential.ApiUsername = dto.Username;
                credential.ApiPasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                credential.MonthlyRequestLimit = monthlyLimit;
                credential.UpdatedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync();
            
            return Json(new { 
                success = true, 
                message = "API kimlik bilgileri oluşturuldu",
                username = credential.ApiUsername,
                monthlyLimit = credential.MonthlyRequestLimit
            });
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { error = $"Veritabanı hatası: {innerMsg}" });
        }
    }
    
    /// <summary>
    /// Toggle API credentials active state
    /// </summary>
    [HttpPatch]
    [Route("api/credentials/toggle")]
    public async Task<IActionResult> ToggleApiCredentials()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Giriş yapmanız gerekiyor" });
            
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        
        var credential = await _context.UserApiCredentials
            .FirstOrDefaultAsync(c => c.UserId == user!.Id && c.OrganizationId == organization.Id);
            
        if (credential == null)
            return NotFound(new { error = "API kimlik bilgisi bulunamadı" });
        
        credential.IsActive = !credential.IsActive;
        credential.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        return Json(new { success = true, isActive = credential.IsActive });
    }
    
    /// <summary>
    /// Delete API credentials
    /// </summary>
    [HttpDelete]
    [Route("api/credentials")]
    public async Task<IActionResult> DeleteApiCredentials()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized(new { error = "Giriş yapmanız gerekiyor" });
            
        var organization = await GetOrCreateOrganizationAsync();
        var user = await _userManager.GetUserAsync(User);
        
        var credential = await _context.UserApiCredentials
            .FirstOrDefaultAsync(c => c.UserId == user!.Id && c.OrganizationId == organization.Id);
            
        if (credential == null)
            return NotFound(new { error = "API kimlik bilgisi bulunamadı" });
        
        _context.UserApiCredentials.Remove(credential);
        await _context.SaveChangesAsync();
        
        return Json(new { success = true, message = "API kimlik bilgileri silindi" });
    }
    
    #endregion
}

// DTOs
public class EmployeeDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? IdentityNo { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Color { get; set; }
    public decimal DailyWorkHours { get; set; } = 8;
    public int WeekendWorkMode { get; set; } = 0; // 0=No weekend, 1=Both days, 2=Saturday only, 3=Saturday specific hours
    public decimal? SaturdayWorkHours { get; set; }
    public string? PositionType { get; set; } // 4A, 4B, 4D, Academic
    public string? AcademicTitle { get; set; } // Prof, Doçent, etc. (only when PositionType is Academic)
    public int ShiftScore { get; set; } = 100; // Default 100
    public bool IsNonHealthServices { get; set; } = false; // SH Dışı
    public int? UnitId { get; set; } // Premium: Unit assignment
}

public class ShiftDto
{
    public int EmployeeId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool SpansNextDay { get; set; }
    public int? ShiftTemplateId { get; set; }
    public int? BreakMinutes { get; set; }
    public bool IsDayOff { get; set; }
    /// <summary>
    /// 0 = Split at midnight (default), 1 = All hours count in current month
    /// </summary>
    public int OvernightHoursMode { get; set; } = 0;
}

public class HolidayDto
{
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HolidayType Type { get; set; }
    public bool IsHalfDay { get; set; }
    public decimal? HalfDayWorkHours { get; set; }
}

public class ShiftTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool SpansNextDay { get; set; }
    public int? BreakMinutes { get; set; }
    public string? Color { get; set; }
    public int DisplayOrder { get; set; }
}

public class SaveScheduleDto
{
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string? Description { get; set; }
    public bool UpdateShiftData { get; set; } = false;
}

public class SavedShiftData
{
    public int EmployeeId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool SpansNextDay { get; set; }
    public int BreakMinutes { get; set; }
    public decimal TotalHours { get; set; }
    public decimal NightHours { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsHoliday { get; set; }
    public bool IsDayOff { get; set; }
    public int OvernightHoursMode { get; set; }
    public int? ShiftTemplateId { get; set; }
}

public class ManualAttendanceDto
{
    public int EmployeeId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string? CheckInTime { get; set; }
    public string? CheckOutTime { get; set; }
    public bool CheckOutToNextDay { get; set; }
    public bool ClearCheckIn { get; set; }
    public bool ClearCheckOut { get; set; }
    public string? Notes { get; set; }
    public AttendanceType Type { get; set; } = AttendanceType.Normal;
}

public class CreateLeaveDto
{
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UnitTypeDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultCoefficient { get; set; } = 1.0m;
    public string? Color { get; set; }
    public string? Icon { get; set; }
}

public class UnitDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? UnitTypeId { get; set; }
    public decimal Coefficient { get; set; } = 1.0m;
    public string? Color { get; set; }
    public bool IsDefault { get; set; }
    public int EmployeeLimit { get; set; } = 0;
}

public class AssignEmployeesDto
{
    public List<int> EmployeeIds { get; set; } = new();
}

public class ApiCredentialDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

