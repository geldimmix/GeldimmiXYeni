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
            .Where(t => t.OrganizationId == organization.Id)
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
            existingShift.OvernightHoursMode = dto.OvernightHoursMode; // FIX: Save overnight hours mode on update
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
        
        // Soft delete
        template.IsActive = false;
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true });
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
    public async Task<IActionResult> Payroll(int? year, int? month, int? nightStartHour, int? nightEndHour, string? source, bool calculate = false, int? loadId = null)
    {
        // Only registered users can access payroll
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Payroll") });
        }

        var selectedYear = year ?? DateTime.Now.Year;
        var selectedMonth = month ?? DateTime.Now.Month;
        var dataSource = source ?? "shift"; // "shift" or "attendance"
        
        var organization = await GetOrCreateOrganizationAsync();
        
        // Use custom night hours or organization defaults
        var nightStart = nightStartHour.HasValue 
            ? new TimeOnly(nightStartHour.Value, 0) 
            : organization.NightStartTime;
        var nightEnd = nightEndHour.HasValue 
            ? new TimeOnly(nightEndHour.Value, 0) 
            : organization.NightEndTime;
        
        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();
            
        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id)
            .Where(h => h.Date.Year == selectedYear && h.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get shifts for current month
        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == selectedYear && s.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get attendance for current month
        var attendances = await _context.TimeAttendances
            .Include(a => a.Employee)
            .Where(a => a.Employee.OrganizationId == organization.Id)
            .Where(a => a.Date.Year == selectedYear && a.Date.Month == selectedMonth)
            .ToListAsync();
        
        // Get overnight shifts from previous month
        var previousMonthLastDay = new DateOnly(selectedYear, selectedMonth, 1).AddDays(-1);
        var previousMonthShifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
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
            SavedPayrolls = savedPayrolls,
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
                    employees, attendances, holidays, organization, selectedYear, selectedMonth, nightStart, nightEnd);
            }
            else
            {
                viewModel.EmployeePayrolls = CalculateEmployeePayrolls(
                    employees, shifts, previousMonthShifts, holidays, 
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
        Organization organization,
        int year, int month,
        TimeOnly nightStart, TimeOnly nightEnd)
    {
        var payrolls = new List<EmployeePayroll>();
        var weekendDays = organization.WeekendDays.Split(',').Select(int.Parse).ToList();

        foreach (var employee in employees)
        {
            var employeeAttendances = attendances.Where(a => a.EmployeeId == employee.Id).ToList();
            
            var payroll = new EmployeePayroll
            {
                Employee = employee,
                ShiftDetails = new List<ShiftDetail>()
            };

            // Calculate required hours for employee
            payroll.RequiredHours = CalculateRequiredHours(employee, year, month, holidays, weekendDays);

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
            var prevMonthShift = previousMonthShifts.FirstOrDefault(s => s.EmployeeId == employee.Id);
            
            var payroll = new EmployeePayroll
            {
                Employee = employee,
                ShiftDetails = new List<ShiftDetail>()
            };

            // Calculate required hours for employee
            payroll.RequiredHours = CalculateRequiredHours(employee, year, month, holidays, weekendDays);

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
    /// </summary>
    private decimal CalculateRequiredHours(Employee employee, int year, int month, List<Holiday> holidays, List<int> weekendDays)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        decimal requiredHours = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var dayOfWeek = date.DayOfWeek;
            var isSaturday = dayOfWeek == DayOfWeek.Saturday;
            var isWeekend = weekendDays.Contains((int)dayOfWeek);
            
            var holiday = holidays.FirstOrDefault(h => h.Date == date);
            
            // Full holiday
            if (holiday != null && !holiday.IsHalfDay)
                continue;
            
            // Half-day holiday
            if (holiday != null && holiday.IsHalfDay && holiday.HalfDayWorkHours.HasValue)
            {
                if (!isWeekend || employee.WeekendWorkMode > 0)
                {
                    requiredHours += holiday.HalfDayWorkHours.Value;
                }
                continue;
            }

            if (isWeekend)
            {
                switch (employee.WeekendWorkMode)
                {
                    case 1: // Works both days
                        requiredHours += employee.DailyWorkHours;
                        break;
                    case 2: // Only Saturday
                        if (isSaturday) requiredHours += employee.DailyWorkHours;
                        break;
                    case 3: // Saturday specific hours
                        if (isSaturday && employee.SaturdayWorkHours.HasValue)
                            requiredHours += employee.SaturdayWorkHours.Value;
                        break;
                }
            }
            else
            {
                requiredHours += employee.DailyWorkHours;
            }
        }

        return requiredHours;
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
    /// Attendance tracking page - only for registered users
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Attendance(int? year, int? month)
    {
        // Only registered users can access attendance
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Attendance") });
        }

        var selectedYear = year ?? DateTime.Now.Year;
        var selectedMonth = month ?? DateTime.Now.Month;
        
        var organization = await GetOrCreateOrganizationAsync();
        
        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();
            
        var attendances = await _context.TimeAttendances
            .Where(a => a.Employee.OrganizationId == organization.Id)
            .Where(a => a.Date.Year == selectedYear && a.Date.Month == selectedMonth)
            .ToListAsync();
            
        var shifts = await _context.Shifts
            .Where(s => s.Employee.OrganizationId == organization.Id)
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

    private int GetEmployeeLimit()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // TODO: Check user plan for premium
            return _configuration.GetValue<int>("AppSettings:RegisteredEmployeeLimit", 10);
        }
        
        return _configuration.GetValue<int>("AppSettings:GuestEmployeeLimit", 5);
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

