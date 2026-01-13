using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Resources;

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

    public AppController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer,
        IConfiguration configuration,
        ILogger<AppController> logger)
    {
        _context = context;
        _userManager = userManager;
        _localizer = localizer;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Main application page
    /// </summary>
    public async Task<IActionResult> Index(int? year, int? month)
    {
        var selectedYear = year ?? DateTime.Now.Year;
        var selectedMonth = month ?? DateTime.Now.Month;
        
        var organization = await GetOrCreateOrganizationAsync();
        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();
            
        var shiftTemplates = await _context.ShiftTemplates
            .Where(t => t.IsGlobal || t.OrganizationId == organization.Id)
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();
            
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id)
            .Where(h => h.Date.Year == selectedYear && h.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get shifts for current month
        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == selectedYear && s.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get overnight shifts from previous month's last day that span into current month
        var previousMonthLastDay = new DateOnly(selectedYear, selectedMonth, 1).AddDays(-1);
        var previousMonthShifts = await _context.Shifts
            .Include(s => s.Employee)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date == previousMonthLastDay && s.SpansNextDay)
            .ToListAsync();

        var viewModel = new AppViewModel
        {
            Organization = organization,
            Employees = employees,
            ShiftTemplates = shiftTemplates,
            Holidays = holidays,
            Shifts = shifts,
            PreviousMonthOvernightShifts = previousMonthShifts,
            SelectedYear = selectedYear,
            SelectedMonth = selectedMonth,
            EmployeeLimit = GetEmployeeLimit(),
            IsRegistered = User.Identity?.IsAuthenticated == true,
            // Premium features only for registered users
            CanUseSmartScheduling = User.Identity?.IsAuthenticated == true,
            CanUseTimesheet = User.Identity?.IsAuthenticated == true,
            CanExportExcel = User.Identity?.IsAuthenticated == true
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
                e.SaturdayWorkHours
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
        var limit = GetEmployeeLimit();
        
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
            Color = dto.Color ?? GetRandomColor(),
            DailyWorkHours = dto.DailyWorkHours > 0 ? dto.DailyWorkHours : 8,
            WeekendWorkMode = dto.WeekendWorkMode,
            SaturdayWorkHours = dto.SaturdayWorkHours
        };
        
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
        
        return Json(new { 
            id = employee.Id, 
            fullName = employee.FullName,
            title = employee.Title,
            identityNo = employee.IdentityNo,
            color = employee.Color,
            dailyWorkHours = employee.DailyWorkHours,
            weekendWorkMode = employee.WeekendWorkMode,
            saturdayWorkHours = employee.SaturdayWorkHours
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
        if (!string.IsNullOrEmpty(dto.Color))
            employee.Color = dto.Color;
        employee.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        return Ok();
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
        employee.IsActive = false;
        employee.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
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
            existingShift.UpdatedAt = DateTime.UtcNow;
            
            if (dto.IsDayOff) {
                existingShift.TotalHours = 0;
                existingShift.NightHours = 0;
            } else {
                CalculateShiftHours(existingShift, organization);
            }
            
            await _context.SaveChangesAsync();
            return Json(new { 
                id = existingShift.Id,
                totalHours = existingShift.TotalHours,
                isDayOff = existingShift.IsDayOff
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
        
        return Json(new { 
            id = shift.Id,
            totalHours = shift.TotalHours,
            isDayOff = shift.IsDayOff
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
            
        _context.Shifts.Remove(shift);
        await _context.SaveChangesAsync();
        
        return Ok();
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
            
        _context.Shifts.Remove(shift);
        await _context.SaveChangesAsync();
        
        return Json(new { 
            totalHours = totalHours,
            isDayOff = isDayOff,
            spansNextDay = spansNextDay
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
                h.IsHalfDay
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
        }
        else
        {
            var holiday = new Holiday
            {
                OrganizationId = organization.Id,
                Date = date,
                Name = dto.Name,
                Type = dto.Type,
                IsHalfDay = dto.IsHalfDay
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
        var templates = await _context.ShiftTemplates
            .Where(t => t.IsGlobal || t.OrganizationId == organization.Id)
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
                t.IsGlobal
            })
            .ToListAsync();
            
        return Json(templates);
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
                    .Include(o => o.Units)
                    .FirstOrDefaultAsync(o => o.UserId == user.Id);
                    
                if (org == null)
                {
                    org = new Organization
                    {
                        UserId = user.Id,
                        Name = user.FullName ?? "My Organization"
                    };
                    
                    // Create default unit
                    org.Units.Add(new Unit { Name = "Default", IsDefault = true });
                    
                    _context.Organizations.Add(org);
                    await _context.SaveChangesAsync();
                }
                
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
            .Include(o => o.Units)
            .FirstOrDefaultAsync(o => o.GuestSessionId == sessionId);
            
        if (guestOrg == null)
        {
            guestOrg = new Organization
            {
                GuestSessionId = sessionId,
                Name = "Guest Organization"
            };
            
            guestOrg.Units.Add(new Unit { Name = "Default", IsDefault = true });
            
            _context.Organizations.Add(guestOrg);
            await _context.SaveChangesAsync();
        }
        
        return guestOrg;
    }

    private int GetEmployeeLimit()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // TODO: Check user plan
            return _configuration.GetValue<int>("AppSettings:FreemiumEmployeeLimit", 25);
        }
        
        return _configuration.GetValue<int>("AppSettings:GuestEmployeeLimit", 10);
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
}

// DTOs
public class EmployeeDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? IdentityNo { get; set; }
    public string? Color { get; set; }
    public decimal DailyWorkHours { get; set; } = 8;
    public int WeekendWorkMode { get; set; } = 0; // 0=No weekend, 1=Both days, 2=Saturday only, 3=Saturday specific hours
    public decimal? SaturdayWorkHours { get; set; }
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
    /// 0 = Split at midnight, 1 = All current month, 2 = All next month
    /// </summary>
    public int OvernightHoursMode { get; set; } = 0;
}

public class HolidayDto
{
    public string Date { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HolidayType Type { get; set; }
    public bool IsHalfDay { get; set; }
}

