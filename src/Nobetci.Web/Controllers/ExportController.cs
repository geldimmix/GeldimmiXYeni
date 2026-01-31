using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Net.Http.Headers;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Models;
using Nobetci.Web.Resources;
using Nobetci.Web.Services;
using System.Globalization;
using System.Text;

namespace Nobetci.Web.Controllers;

[Route("api/export")]
public class ExportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IBordroCalculator _bordroCalculator;
    private readonly IBordroHesaplamaService _bordroHesaplamaService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExportController> _logger;

    public ExportController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer,
        IBordroCalculator bordroCalculator,
        IBordroHesaplamaService bordroHesaplamaService,
        IConfiguration configuration,
        ILogger<ExportController> logger)
    {
        _context = context;
        _userManager = userManager;
        _localizer = localizer;
        _bordroCalculator = bordroCalculator;
        _bordroHesaplamaService = bordroHesaplamaService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Returns Excel file with properly encoded Turkish filename
    /// </summary>
    private FileContentResult ExcelFile(byte[] fileContents, string fileName)
    {
        var cd = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = fileName
        };
        Response.Headers.Append(HeaderNames.ContentDisposition, cd.ToString());
        return File(fileContents, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [HttpGet("excel")]
    public async Task<IActionResult> ExportExcel(int year, int month)
    {
        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        try { await _bordroHesaplamaService.EnsureBordroSabitleriAsync(organization.Id); }
        catch (Exception ex) { _logger.LogWarning(ex, "ExportExcel: EnsureBordroSabitleri failed OrgId={OrgId}, using config defaults", organization.Id); }

        // Get current culture for localization
        var culture = CultureInfo.CurrentUICulture;
        var isTurkish = culture.TwoLetterISOLanguageName == "tr";

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        var units = await _context.Units
            .Include(u => u.UnitType)
            .Where(u => u.OrganizationId == organization.Id && u.IsActive)
            .ToListAsync();

        var source = "shift";
        var nightStartHour = organization.NightStartTime.Hour;
        var nightEndHour = organization.NightEndTime.Hour;
        var nightStart = new TimeOnly(nightStartHour, 0);
        var nightEnd = new TimeOnly(nightEndHour, 0);
        var attendances = new List<TimeAttendance>();

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var firstDayOfMonth = new DateOnly(year, month, 1);
        var lastDayOfMonth = new DateOnly(year, month, daysInMonth);
        
        // Get previous month's last day to check for overnight shifts spilling into this month
        var prevMonth = firstDayOfMonth.AddDays(-1);
        
        // Get shifts for current month
        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .ToListAsync();
            
        // Get overnight shifts from previous month that may spill into this month
        var prevMonthOvernightShifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date == prevMonth && s.SpansNextDay)
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
        
        // Get localized month name
        var payrollOptions = GetPayrollOptions();
        var bordroOptions = await GetBordroOptionsAsync(organization.Id);

        var employeePayrolls = source == "attendance"
            ? CalculatePayrollFromAttendance(
                employees,
                attendances,
                holidays,
                leaves,
                organization,
                year,
                month,
                nightStart,
                nightEnd,
                payrollOptions,
                units)
            : CalculateEmployeePayrolls(
                employees,
                shifts,
                prevMonthOvernightShifts,
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

        var monthDate = new DateTime(year, month, 1);
        var monthName = monthDate.ToString("MMMM yyyy", culture);

        // Localized texts
        var sheetName = isTurkish ? $"Nöbet Listesi - {monthName}" : $"Shift Schedule - {monthName}";
        var employeeHeader = isTurkish ? "Personel" : "Employee";
        var totalHoursHeader = isTurkish ? "Toplam" : "Total";
        var hoursAbbrev = isTurkish ? "s" : "h"; // saat / hours abbreviation

        // Day name abbreviations
        var dayNames = isTurkish 
            ? new[] { "Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt" }
            : new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName.Length > 31 ? sheetName.Substring(0, 31) : sheetName);

        // Header row - Employee column
        worksheet.Cell(1, 1).Value = employeeHeader;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        worksheet.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Header row - Day columns with day names
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var holiday = holidays.FirstOrDefault(h => h.Date == date);
            var isWeekend = IsWeekend(date, organization);
            var dayName = dayNames[(int)date.DayOfWeek];

            var cell = worksheet.Cell(1, day + 1);
            cell.Value = $"{day}\n{dayName}";
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;

            if (holiday != null)
            {
                cell.Style.Fill.BackgroundColor = XLColor.Yellow;
                cell.CreateComment().AddText(holiday.Name);
            }
            else if (isWeekend)
            {
                cell.Style.Fill.BackgroundColor = XLColor.LightPink;
            }
        }

        // Total column header
        worksheet.Cell(1, daysInMonth + 2).Value = totalHoursHeader;
        worksheet.Cell(1, daysInMonth + 2).Style.Font.Bold = true;
        worksheet.Cell(1, daysInMonth + 2).Style.Fill.BackgroundColor = XLColor.LightGray;
        worksheet.Cell(1, daysInMonth + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Cell(1, daysInMonth + 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Employee rows
        int row = 2;
        foreach (var employee in employees)
        {
            worksheet.Cell(row, 1).Value = employee.FullName;
            worksheet.Cell(row, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            if (!string.IsNullOrEmpty(employee.Title))
            {
                worksheet.Cell(row, 1).CreateComment().AddText(employee.Title);
            }

            decimal totalWorkedHours = 0;
            
            // Add hours from overnight shift that spilled from previous month (day 1 only)
            var prevMonthShiftForEmployee = prevMonthOvernightShifts.FirstOrDefault(s => s.EmployeeId == employee.Id);
            if (prevMonthShiftForEmployee != null && !prevMonthShiftForEmployee.IsDayOff)
            {
                var spilledHours = CalculateSpilledHoursFromPreviousMonth(prevMonthShiftForEmployee);
                totalWorkedHours += spilledHours;
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateOnly(year, month, day);
                var shift = shifts.FirstOrDefault(s => s.EmployeeId == employee.Id && s.Date == date);
                var holiday = holidays.FirstOrDefault(h => h.Date == date);
                var isWeekend = IsWeekend(date, organization);

                var cell = worksheet.Cell(row, day + 1);
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                if (holiday != null)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                }
                else if (isWeekend)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.MistyRose;
                }

                if (shift != null)
                {
                    if (shift.IsDayOff)
                    {
                        // Day off - show X or İzin
                        cell.Value = isTurkish ? "İzin" : "Off";
                        cell.Style.Font.FontSize = 9;
                        cell.Style.Font.Italic = true;
                    }
                    else
                    {
                        // Build shift text: time range + hours
                        var timeText = $"{shift.StartTime:HH:mm}-{shift.EndTime:HH:mm}";
                        if (shift.SpansNextDay)
                        {
                            timeText += "↓";
                        }
                        
                        // Calculate hours for this month considering overnight mode
                        var hoursForThisMonth = CalculateShiftHoursForMonth(shift, year, month);
                        
                        // Add hours in parentheses (display total hours, not split hours)
                        var hoursText = shift.TotalHours % 1 == 0 
                            ? $"({(int)shift.TotalHours}{hoursAbbrev})"
                            : $"({shift.TotalHours:0.#}{hoursAbbrev})";
                        
                        cell.Value = $"{timeText}\n{hoursText}";
                        cell.Style.Font.FontSize = 9;
                        cell.Style.Alignment.WrapText = true;
                        
                        // Add only the hours that count for THIS month
                        totalWorkedHours += hoursForThisMonth;
                    }
                }
            }

            // Calculate required hours for this employee
            var requiredHours = CalculateRequiredHours(employee, year, month, holidays, organization);
            var difference = totalWorkedHours - requiredHours;
            var diffSign = difference >= 0 ? "+" : "";
            
            // Format: worked / required (difference)
            var workedDisplay = totalWorkedHours % 1 == 0 ? $"{(int)totalWorkedHours}" : $"{totalWorkedHours:0.#}";
            var requiredDisplay = requiredHours % 1 == 0 ? $"{(int)requiredHours}" : $"{requiredHours:0.#}";
            var diffDisplay = difference % 1 == 0 ? $"{diffSign}{(int)difference}" : $"{diffSign}{difference:0.#}";
            
            var totalCell = worksheet.Cell(row, daysInMonth + 2);
            totalCell.Value = $"{workedDisplay}\n/{requiredDisplay}\n({diffDisplay})";
            totalCell.Style.Font.Bold = true;
            totalCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            totalCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            totalCell.Style.Alignment.WrapText = true;
            
            // Color code based on difference
            if (difference > 0)
            {
                totalCell.Style.Font.FontColor = XLColor.Green;
            }
            else if (difference < 0)
            {
                totalCell.Style.Font.FontColor = XLColor.Red;
            }

            row++;
        }

        // Set row heights for better display
        worksheet.Row(1).Height = 35;
        for (int r = 2; r < row; r++)
        {
            worksheet.Row(r).Height = 45;
        }

        // Auto-fit columns
        worksheet.Column(1).Width = 22;
        for (int col = 2; col <= daysInMonth + 1; col++)
        {
            worksheet.Column(col).Width = 12;
        }
        worksheet.Column(daysInMonth + 2).Width = 10;

        // Freeze first row and column
        worksheet.SheetView.FreezeRows(1);
        worksheet.SheetView.FreezeColumns(1);

        // Add borders
        var dataRange = worksheet.Range(1, 1, row - 1, daysInMonth + 2);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Generate file
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        // Localized filename
        var fileName = isTurkish
            ? $"Nobet_Listesi_{year}_{month:00}.xlsx"
            : $"Shift_Schedule_{year}_{month:00}.xlsx";
            
        return ExcelFile(stream.ToArray(), fileName);
    }

    /// <summary>
    /// Calculate required work hours for an employee in a given month
    /// </summary>
    private decimal CalculateRequiredHours(Employee employee, int year, int month, List<Holiday> holidays, Organization organization)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        decimal requiredHours = 0;
        var weekendDays = organization.WeekendDays.Split(',').Select(int.Parse).ToList();

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var dayOfWeek = date.DayOfWeek;
            var isSaturday = dayOfWeek == DayOfWeek.Saturday;
            var isSunday = dayOfWeek == DayOfWeek.Sunday;
            var isWeekend = weekendDays.Contains((int)dayOfWeek);
            
            // Check for holiday
            var holiday = holidays.FirstOrDefault(h => h.Date == date);
            
            // Full holiday - no work required (unless half-day)
            if (holiday != null && !holiday.IsHalfDay)
            {
                continue;
            }
            
            // Half-day holiday
            if (holiday != null && holiday.IsHalfDay)
            {
                // Check if employee should work on this day
                bool shouldWorkThisDay = ShouldEmployeeWorkOnDay(employee, dayOfWeek, isWeekend, isSaturday, isSunday);
                if (shouldWorkThisDay && holiday.HalfDayWorkHours.HasValue)
                {
                    requiredHours += holiday.HalfDayWorkHours.Value;
                }
                continue;
            }
            
            // Regular day - check weekend work mode
            if (isWeekend)
            {
                // WeekendWorkMode: 0=No weekend, 1=Both days, 2=Only Saturday, 3=Saturday specific hours
                switch (employee.WeekendWorkMode)
                {
                    case 0: // Does not work on weekends
                        break;
                    case 1: // Works both days
                        requiredHours += employee.DailyWorkHours;
                        break;
                    case 2: // Only Saturday
                        if (isSaturday)
                        {
                            requiredHours += employee.DailyWorkHours;
                        }
                        break;
                    case 3: // Saturday specific hours
                        if (isSaturday && employee.SaturdayWorkHours.HasValue)
                        {
                            requiredHours += employee.SaturdayWorkHours.Value;
                        }
                        break;
                }
            }
            else
            {
                // Weekday - add daily work hours
                requiredHours += employee.DailyWorkHours;
            }
        }

        return requiredHours;
    }

    /// <summary>
    /// Check if employee should work on a specific day based on their weekend work mode
    /// </summary>
    private bool ShouldEmployeeWorkOnDay(Employee employee, DayOfWeek dayOfWeek, bool isWeekend, bool isSaturday, bool isSunday)
    {
        if (!isWeekend)
        {
            return true; // Weekdays are always work days
        }

        // Weekend logic
        switch (employee.WeekendWorkMode)
        {
            case 0: // Does not work on weekends
                return false;
            case 1: // Works both days
                return true;
            case 2: // Only Saturday
                return isSaturday;
            case 3: // Saturday specific hours
                return isSaturday;
            default:
                return false;
        }
    }

    private async Task<Organization?> GetOrganizationAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                return await _context.Organizations
                    .FirstOrDefaultAsync(o => o.UserId == user.Id);
            }
        }

        var sessionId = HttpContext.Session.GetString("GuestSessionId");
        if (!string.IsNullOrEmpty(sessionId))
        {
            return await _context.Organizations
                .FirstOrDefaultAsync(o => o.GuestSessionId == sessionId);
        }

        return null;
    }

    private static bool IsWeekend(DateOnly date, Organization org)
    {
        var weekendDays = org.WeekendDays.Split(',').Select(int.Parse).ToList();
        return weekendDays.Contains((int)date.DayOfWeek);
    }
    
    /// <summary>
    /// Calculate how many hours of a shift count for the specified month
    /// Handles overnight shifts that span month boundaries based on OvernightHoursMode
    /// Mode 0 = Split at midnight, Mode 1 = All hours this month
    /// </summary>
    private decimal CalculateShiftHoursForMonth(Shift shift, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lastDayOfMonth = new DateOnly(year, month, daysInMonth);
        
        // If shift doesn't span next day, all hours count for this month
        if (!shift.SpansNextDay)
        {
            return shift.TotalHours;
        }
        
        // Check if shift spans to next month (shift on last day of month)
        bool spansToNextMonth = shift.Date == lastDayOfMonth;
        
        if (!spansToNextMonth)
        {
            // Shift spans to next day but still within the same month
            return shift.TotalHours;
        }
        
        // Shift spans from last day of month to first day of next month
        // OvernightHoursMode: 0 = Split at midnight, 1 = All hours this month
        if (shift.OvernightHoursMode == 0)
        {
            // Split at midnight - only hours before midnight count for this month
            return CalculateHoursBeforeMidnight(shift);
        }
        else
        {
            // Mode 1: All hours count in current month
            return shift.TotalHours;
        }
    }
    
    /// <summary>
    /// Calculate hours from a previous month's overnight shift that spill into this month
    /// Mode 0 = Split at midnight (add hours after midnight), Mode 1 = All hours in previous month (add nothing)
    /// </summary>
    private decimal CalculateSpilledHoursFromPreviousMonth(Shift shift)
    {
        // OvernightHoursMode: 0 = Split at midnight, 1 = All hours in start month
        if (shift.OvernightHoursMode == 0)
        {
            // Split at midnight - hours after midnight count for this month
            return CalculateHoursAfterMidnight(shift);
        }
        else
        {
            // Mode 1: All hours counted in previous month, nothing to add
            return 0;
        }
    }
    
    /// <summary>
    /// Calculate hours worked before midnight (from StartTime to 00:00)
    /// </summary>
    private decimal CalculateHoursBeforeMidnight(Shift shift)
    {
        // Hours from start time to midnight (24:00)
        var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
        var minutesUntilMidnight = (24 * 60) - startMinutes;
        
        // Subtract proportional break time
        // If total shift is X hours with Y break minutes, before midnight gets proportional break
        var totalShiftMinutes = (int)(shift.TotalHours * 60) + shift.BreakMinutes;
        var breakProportion = totalShiftMinutes > 0 
            ? (decimal)minutesUntilMidnight / totalShiftMinutes 
            : 0;
        var breakBeforeMidnight = (int)(shift.BreakMinutes * breakProportion);
        
        var hoursBeforeMidnight = (minutesUntilMidnight - breakBeforeMidnight) / 60m;
        return Math.Max(0, hoursBeforeMidnight);
    }
    
    /// <summary>
    /// Calculate hours worked after midnight (from 00:00 to EndTime)
    /// </summary>
    private decimal CalculateHoursAfterMidnight(Shift shift)
    {
        // Hours from midnight to end time
        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
        
        // Subtract proportional break time
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

    /// <summary>
    /// Export payroll/timesheet to Excel
    /// </summary>
    [HttpGet("payroll")]
    public async Task<IActionResult> ExportPayroll(int year, int month, string source = "shift", int nightStartHour = 22, int nightEndHour = 6)
    {
        // Only registered users
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        try { await _bordroHesaplamaService.EnsureBordroSabitleriAsync(organization.Id); }
        catch (Exception ex) { _logger.LogWarning(ex, "ExportPayroll: EnsureBordroSabitleri failed OrgId={OrgId}", organization.Id); }

        var culture = CultureInfo.CurrentUICulture;
        var isTurkish = culture.TwoLetterISOLanguageName == "tr";

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        var units = await _context.Units
            .Include(u => u.UnitType)
            .Where(u => u.OrganizationId == organization.Id && u.IsActive)
            .ToListAsync();

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var firstDayOfMonth = new DateOnly(year, month, 1);
        var prevMonth = firstDayOfMonth.AddDays(-1);

        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .ToListAsync();

        var prevMonthOvernightShifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date == prevMonth && s.SpansNextDay)
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

        var weekendDays = organization.WeekendDays.Split(',').Select(int.Parse).ToList();
        var nightStart = new TimeOnly(nightStartHour, 0);
        var nightEnd = new TimeOnly(nightEndHour, 0);

        // Get attendance if source is attendance
        var attendances = source == "attendance" 
            ? await _context.TimeAttendances
                .Include(a => a.Employee)
                .Where(a => a.Employee.OrganizationId == organization.Id)
                .Where(a => a.Date.Year == year && a.Date.Month == month)
                .ToListAsync()
            : new List<TimeAttendance>();

        var payrollOptions = GetPayrollOptions();
        var bordroOptions = await GetBordroOptionsAsync(organization.Id);
        var employeePayrolls = source == "attendance"
            ? CalculatePayrollFromAttendance(
                employees,
                attendances,
                holidays,
                leaves,
                organization,
                year,
                month,
                nightStart,
                nightEnd,
                payrollOptions,
                units)
            : CalculateEmployeePayrolls(
                employees,
                shifts,
                prevMonthOvernightShifts,
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

        var monthDate = new DateTime(year, month, 1);
        var monthName = monthDate.ToString("MMMM yyyy", culture);

        // Sheet name
        var sourceText = source == "attendance" 
            ? (isTurkish ? "Mesai Takip" : "Attendance") 
            : (isTurkish ? "Nöbet" : "Shift");
        var sheetName = isTurkish ? $"Puantaj - {monthName}" : $"Payroll - {monthName}";

        var headers = isTurkish
            ? new[]
            {
                "Personel", "Ünvan", "Çalışılan Gün", "Çalışılan Saat", "Hedef Saat",
                "Fazla Mesai", "Normal F.Mesai", "Yoğun F.Mesai",
                "Gece Çalışma", "Hafta Sonu",
                "Resmi Tatil", "Normal Tatil", "Yoğun Tatil", "Bayram Farkı",
                "İzin Günü"
            }
            : new[]
            {
                "Employee", "Title", "Days Worked", "Hours Worked", "Target Hours",
                "Overtime", "Normal OT", "ICU OT",
                "Night Hours", "Weekend Hours",
                "Holiday Hours", "Normal Holiday", "ICU Holiday", "Holiday Diff",
                "Days Off"
            };

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName.Length > 31 ? sheetName.Substring(0, 31) : sheetName);

        // Add info header
        worksheet.Cell(1, 1).Value = isTurkish ? "Puantaj Raporu" : "Payroll Report";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Range(1, 1, 1, 4).Merge();

        worksheet.Cell(2, 1).Value = isTurkish ? $"Dönem: {monthName}" : $"Period: {monthName}";
        worksheet.Cell(2, 5).Value = isTurkish 
            ? $"Kaynak: {sourceText} | Gece: {nightStartHour:00}:00 - {nightEndHour:00}:00" 
            : $"Source: {sourceText} | Night: {nightStartHour:00}:00 - {nightEndHour:00}:00";

        // Column headers
        int headerRow = 4;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Employee data
        int row = headerRow + 1;
        foreach (var payroll in employeePayrolls.OrderBy(p => p.Employee.FullName))
        {
            worksheet.Cell(row, 1).Value = payroll.Employee.FullName;
            worksheet.Cell(row, 2).Value = payroll.Employee.Title ?? "";
            worksheet.Cell(row, 3).Value = payroll.WorkedDays;
            worksheet.Cell(row, 4).Value = (double)payroll.TotalWorkedHours;
            worksheet.Cell(row, 5).Value = (double)payroll.RequiredHours;
            worksheet.Cell(row, 6).Value = (double)payroll.OvertimeHours;
            worksheet.Cell(row, 7).Value = (double)payroll.NormalOvertimeHours;
            worksheet.Cell(row, 8).Value = (double)payroll.IntensiveOvertimeHours;
            worksheet.Cell(row, 9).Value = (double)payroll.NightHours;
            worksheet.Cell(row, 10).Value = (double)payroll.WeekendHours;
            worksheet.Cell(row, 11).Value = (double)payroll.HolidayHours;
            worksheet.Cell(row, 12).Value = (double)payroll.NormalHolidayHours;
            worksheet.Cell(row, 13).Value = (double)payroll.IntensiveHolidayHours;
            worksheet.Cell(row, 14).Value = (double)payroll.HolidayDifferenceHours;
            worksheet.Cell(row, 15).Value = payroll.DayOffCount;

            // Format numbers
            for (int col = 4; col <= 14; col++)
            {
                worksheet.Cell(row, col).Style.NumberFormat.Format = "0.0";
            }
            
            // Highlight overtime in green if positive
            if (payroll.OvertimeHours > 0)
            {
                worksheet.Cell(row, 6).Style.Font.FontColor = XLColor.Green;
                worksheet.Cell(row, 6).Style.Font.Bold = true;
            }

            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add borders
        var dataRange = worksheet.Range(headerRow, 1, row - 1, headers.Length);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        if (bordro4A.Any())
        {
            AddBordro4ASheet(workbook, bordro4A, isTurkish);
        }
        if (bordro4B.Any())
        {
            AddBordro4BSheet(workbook, bordro4B, isTurkish);
        }

        // Generate file
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = isTurkish
            ? $"Puantaj_{year}_{month:00}.xlsx"
            : $"Payroll_{year}_{month:00}.xlsx";

        return ExcelFile(stream.ToArray(), fileName);
    }

    /// <summary>
    /// Export saved payroll to Excel
    /// </summary>
    [HttpGet("payroll-saved/{id}")]
    public async Task<IActionResult> ExportSavedPayroll(int id)
    {
        // Only registered users
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var organization = await GetOrganizationAsync();
        if (organization == null)
            return NotFound();

        try { await _bordroHesaplamaService.EnsureBordroSabitleriAsync(organization.Id); }
        catch (Exception ex) { _logger.LogWarning(ex, "ExportSavedPayroll: EnsureBordroSabitleri failed OrgId={OrgId}", organization.Id); }

        var savedPayroll = await _context.SavedPayrolls
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organization.Id);

        if (savedPayroll == null)
            return NotFound();

        var culture = CultureInfo.CurrentUICulture;
        var isTurkish = culture.TwoLetterISOLanguageName == "tr";

        // Parse the saved payroll data
        var entries = System.Text.Json.JsonSerializer.Deserialize<List<SavedPayrollEntry>>(savedPayroll.PayrollDataJson) 
            ?? new List<SavedPayrollEntry>();

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        var units = await _context.Units
            .Include(u => u.UnitType)
            .Where(u => u.OrganizationId == organization.Id && u.IsActive)
            .ToListAsync();

        var year = savedPayroll.Year;
        var month = savedPayroll.Month;
        var firstDayOfMonth = new DateOnly(year, month, 1);
        var prevMonth = firstDayOfMonth.AddDays(-1);

        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .ToListAsync();

        var prevMonthOvernightShifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date == prevMonth && s.SpansNextDay)
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

        var attendances = savedPayroll.DataSource == "attendance"
            ? await _context.TimeAttendances
                .Include(a => a.Employee)
                .Where(a => a.Employee.OrganizationId == organization.Id)
                .Where(a => a.Date.Year == year && a.Date.Month == month)
                .ToListAsync()
            : new List<TimeAttendance>();

        var nightStart = new TimeOnly(savedPayroll.NightStartHour, 0);
        var nightEnd = new TimeOnly(savedPayroll.NightEndHour, 0);

        var payrollOptions = GetPayrollOptions();
        var bordroOptions = await GetBordroOptionsAsync(organization.Id);

        var employeePayrolls = savedPayroll.DataSource == "attendance"
            ? CalculatePayrollFromAttendance(
                employees,
                attendances,
                holidays,
                leaves,
                organization,
                year,
                month,
                nightStart,
                nightEnd,
                payrollOptions,
                units)
            : CalculateEmployeePayrolls(
                employees,
                shifts,
                prevMonthOvernightShifts,
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

        var monthDate = new DateTime(savedPayroll.Year, savedPayroll.Month, 1);
        var monthName = monthDate.ToString("MMMM yyyy", culture);

        var sourceText = savedPayroll.DataSource == "attendance" 
            ? (isTurkish ? "Mesai Takip" : "Attendance") 
            : (isTurkish ? "Nöbet" : "Shift");
        var summarySheetName = isTurkish ? "Özet" : "Summary";
        var detailSheetName = isTurkish ? "Detay" : "Details";

        var headers = isTurkish
            ? new[]
            {
                "Personel", "Ünvan", "Çalışılan Gün", "Çalışılan Saat", "Hedef Saat",
                "Fazla Mesai", "Normal F.Mesai", "Yoğun F.Mesai",
                "Gece Çalışma", "Hafta Sonu",
                "Resmi Tatil", "Normal Tatil", "Yoğun Tatil", "Bayram Farkı",
                "İzin Günü"
            }
            : new[]
            {
                "Employee", "Title", "Days Worked", "Hours Worked", "Target Hours",
                "Overtime", "Normal OT", "ICU OT",
                "Night Hours", "Weekend Hours",
                "Holiday Hours", "Normal Holiday", "ICU Holiday", "Holiday Diff",
                "Days Off"
            };

        using var workbook = new XLWorkbook();
        
        // ========== SUMMARY SHEET ==========
        var summarySheet = workbook.Worksheets.Add(summarySheetName);

        // Add info header
        summarySheet.Cell(1, 1).Value = isTurkish ? "Puantaj Raporu" : "Payroll Report";
        summarySheet.Cell(1, 1).Style.Font.Bold = true;
        summarySheet.Cell(1, 1).Style.Font.FontSize = 14;
        summarySheet.Range(1, 1, 1, 4).Merge();

        summarySheet.Cell(2, 1).Value = isTurkish ? $"Dönem: {monthName}" : $"Period: {monthName}";
        summarySheet.Cell(2, 5).Value = isTurkish 
            ? $"Kaynak: {sourceText} | Gece: {savedPayroll.NightStartHour:00}:00 - {savedPayroll.NightEndHour:00}:00" 
            : $"Source: {sourceText} | Night: {savedPayroll.NightStartHour:00}:00 - {savedPayroll.NightEndHour:00}:00";
        
        summarySheet.Cell(3, 1).Value = isTurkish ? $"Kayıt: {savedPayroll.Name}" : $"Record: {savedPayroll.Name}";
        summarySheet.Cell(3, 5).Value = isTurkish 
            ? $"Oluşturulma: {savedPayroll.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}" 
            : $"Created: {savedPayroll.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}";

        // Column headers
        int headerRow = 5;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = summarySheet.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Employee data
        int row = headerRow + 1;
        foreach (var payroll in employeePayrolls.OrderBy(e => e.Employee.FullName))
        {
            summarySheet.Cell(row, 1).Value = payroll.Employee.FullName;
            summarySheet.Cell(row, 2).Value = payroll.Employee.Title ?? "";
            summarySheet.Cell(row, 3).Value = payroll.WorkedDays;
            summarySheet.Cell(row, 4).Value = (double)payroll.TotalWorkedHours;
            summarySheet.Cell(row, 5).Value = (double)payroll.RequiredHours;
            summarySheet.Cell(row, 6).Value = (double)payroll.OvertimeHours;
            summarySheet.Cell(row, 7).Value = (double)payroll.NormalOvertimeHours;
            summarySheet.Cell(row, 8).Value = (double)payroll.IntensiveOvertimeHours;
            summarySheet.Cell(row, 9).Value = (double)payroll.NightHours;
            summarySheet.Cell(row, 10).Value = (double)payroll.WeekendHours;
            summarySheet.Cell(row, 11).Value = (double)payroll.HolidayHours;
            summarySheet.Cell(row, 12).Value = (double)payroll.NormalHolidayHours;
            summarySheet.Cell(row, 13).Value = (double)payroll.IntensiveHolidayHours;
            summarySheet.Cell(row, 14).Value = (double)payroll.HolidayDifferenceHours;
            summarySheet.Cell(row, 15).Value = payroll.DayOffCount;

            // Format numbers
            for (int col = 4; col <= 14; col++)
            {
                summarySheet.Cell(row, col).Style.NumberFormat.Format = "0.0";
            }
            
            // Highlight overtime in green
            if (payroll.OvertimeHours > 0)
            {
                summarySheet.Cell(row, 6).Style.Font.FontColor = XLColor.Green;
                summarySheet.Cell(row, 6).Style.Font.Bold = true;
            }

            row++;
        }

        // Auto-fit columns
        summarySheet.Columns().AdjustToContents();

        // Add borders
        if (employeePayrolls.Any())
        {
            var dataRange = summarySheet.Range(headerRow, 1, row - 1, headers.Length);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        // ========== DAILY DETAIL SHEET ==========
        var detailSheet = workbook.Worksheets.Add(detailSheetName);
        
        var detailHeaders = isTurkish
            ? new[] { "Personel", "Tarih", "Gün", "Giriş", "Çıkış", "Saat", "Gece", "H.Sonu", "Tatil", "İzin", "Not" }
            : new[] { "Employee", "Date", "Day", "In", "Out", "Hours", "Night", "Wknd", "Hol", "Off", "Note" };
        
        var dayNames = isTurkish 
            ? new[] { "Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt" }
            : new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        // Detail header row
        for (int i = 0; i < detailHeaders.Length; i++)
        {
            var cell = detailSheet.Cell(1, i + 1);
            cell.Value = detailHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int detailRow = 2;
        foreach (var entry in entries.OrderBy(e => e.EmployeeName))
        {
            if (entry.DailyEntries == null || !entry.DailyEntries.Any())
                continue;

            foreach (var daily in entry.DailyEntries.OrderBy(d => d.Date))
            {
                DateOnly.TryParse(daily.Date, out var date);
                var dayOfWeek = date != default ? (int)date.DayOfWeek : 0;
                
                detailSheet.Cell(detailRow, 1).Value = entry.EmployeeName;
                detailSheet.Cell(detailRow, 2).Value = daily.Date;
                detailSheet.Cell(detailRow, 3).Value = dayNames[dayOfWeek];
                detailSheet.Cell(detailRow, 4).Value = daily.StartTime ?? "-";
                detailSheet.Cell(detailRow, 5).Value = daily.EndTime ?? "-";
                detailSheet.Cell(detailRow, 6).Value = daily.IsDayOff ? "-" : (double)daily.Hours;
                detailSheet.Cell(detailRow, 7).Value = daily.NightHours > 0 ? (double)daily.NightHours : 0;
                detailSheet.Cell(detailRow, 8).Value = daily.IsWeekend ? "✓" : "";
                detailSheet.Cell(detailRow, 9).Value = daily.IsHoliday ? "✓" : "";
                detailSheet.Cell(detailRow, 10).Value = daily.IsDayOff ? "✓" : "";
                detailSheet.Cell(detailRow, 11).Value = daily.Note ?? "";

                // Highlight weekends and holidays
                if (daily.IsWeekend)
                {
                    detailSheet.Range(detailRow, 1, detailRow, detailHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightYellow;
                }
                if (daily.IsHoliday)
                {
                    detailSheet.Range(detailRow, 1, detailRow, detailHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightGreen;
                }
                if (daily.IsDayOff)
                {
                    detailSheet.Range(detailRow, 1, detailRow, detailHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightPink;
                }

                // Format numbers
                if (!daily.IsDayOff)
                {
                    detailSheet.Cell(detailRow, 6).Style.NumberFormat.Format = "0.0";
                }
                detailSheet.Cell(detailRow, 7).Style.NumberFormat.Format = "0.0";

                detailRow++;
            }
        }

        // Auto-fit detail columns
        detailSheet.Columns().AdjustToContents();

        // Add borders to detail sheet
        if (detailRow > 2)
        {
            var detailRange = detailSheet.Range(1, 1, detailRow - 1, detailHeaders.Length);
            detailRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            detailRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        if (bordro4A.Any())
        {
            AddBordro4ASheet(workbook, bordro4A, isTurkish);
        }
        if (bordro4B.Any())
        {
            AddBordro4BSheet(workbook, bordro4B, isTurkish);
        }

        // Generate file
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = isTurkish
            ? $"Puantaj_{savedPayroll.Name.Replace(" ", "_")}_{savedPayroll.Year}_{savedPayroll.Month:00}.xlsx"
            : $"Payroll_{savedPayroll.Name.Replace(" ", "_")}_{savedPayroll.Year}_{savedPayroll.Month:00}.xlsx";

        return ExcelFile(stream.ToArray(), fileName);
    }

    private (int workedDays, int totalWorkDays) CalculateWorkDays(Employee employee, List<Shift> shifts, int year, int month, List<Holiday> holidays, List<int> weekendDays)
    {
        var workedDays = shifts.Count(s => !s.IsDayOff);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        int totalWorkDays = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var dayOfWeek = date.DayOfWeek;
            var isSaturday = dayOfWeek == DayOfWeek.Saturday;
            var isWeekend = weekendDays.Contains((int)dayOfWeek);
            var holiday = holidays.FirstOrDefault(h => h.Date == date);

            if (holiday != null && !holiday.IsHalfDay) continue;

            if (isWeekend)
            {
                if (employee.WeekendWorkMode == 1 || 
                    (employee.WeekendWorkMode >= 2 && isSaturday))
                    totalWorkDays++;
            }
            else
            {
                totalWorkDays++;
            }
        }

        return (workedDays, totalWorkDays);
    }

    private decimal CalculateRequiredHoursForExport(Employee employee, int year, int month, List<Holiday> holidays, List<int> weekendDays)
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

            if (holiday != null && !holiday.IsHalfDay) continue;

            if (holiday != null && holiday.IsHalfDay && holiday.HalfDayWorkHours.HasValue)
            {
                if (!isWeekend || employee.WeekendWorkMode > 0)
                    requiredHours += holiday.HalfDayWorkHours.Value;
                continue;
            }

            if (isWeekend)
            {
                switch (employee.WeekendWorkMode)
                {
                    case 1: requiredHours += employee.DailyWorkHours; break;
                    case 2: if (isSaturday) requiredHours += employee.DailyWorkHours; break;
                    case 3: if (isSaturday && employee.SaturdayWorkHours.HasValue) requiredHours += employee.SaturdayWorkHours.Value; break;
                }
            }
            else
            {
                requiredHours += employee.DailyWorkHours;
            }
        }

        return requiredHours;
    }

    private decimal CalculateWorkedHoursForExport(List<Shift> shifts, Shift? prevMonthShift, int year, int month)
    {
        decimal total = 0;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lastDayOfMonth = new DateOnly(year, month, daysInMonth);

        // Add hours from previous month overnight shift (if split mode)
        if (prevMonthShift != null && !prevMonthShift.IsDayOff && prevMonthShift.OvernightHoursMode == 0)
        {
            total += CalculateHoursAfterMidnight(prevMonthShift);
        }

        foreach (var shift in shifts.Where(s => !s.IsDayOff))
        {
            if (shift.SpansNextDay && shift.Date == lastDayOfMonth && shift.OvernightHoursMode == 0)
            {
                total += CalculateHoursBeforeMidnight(shift);
            }
            else
            {
                total += shift.TotalHours;
            }
        }

        return total;
    }

    private decimal CalculateNightHoursForExport(List<Shift> shifts, Shift? prevMonthShift, TimeOnly nightStart, TimeOnly nightEnd, int year, int month)
    {
        decimal nightHours = 0;

        // Add night hours from previous month spill
        if (prevMonthShift != null && !prevMonthShift.IsDayOff && prevMonthShift.OvernightHoursMode == 0)
        {
            var endMinutes = prevMonthShift.EndTime.Hour * 60 + prevMonthShift.EndTime.Minute;
            var nightEndMinutes = nightEnd.Hour * 60 + nightEnd.Minute;
            nightHours += Math.Min(endMinutes, nightEndMinutes) / 60m;
        }

        foreach (var shift in shifts.Where(s => !s.IsDayOff))
        {
            nightHours += CalculateShiftNightHours(shift, nightStart, nightEnd);
        }

        return nightHours;
    }

    private decimal CalculateShiftNightHours(Shift shift, TimeOnly nightStart, TimeOnly nightEnd)
    {
        return CalculateNightHoursFromTimes(shift.StartTime, shift.EndTime, shift.SpansNextDay, nightStart, nightEnd);
    }

    private decimal CalculateNightHoursFromTimes(TimeOnly startTime, TimeOnly endTime, bool spansNextDay, TimeOnly nightStart, TimeOnly nightEnd)
    {
        decimal nightMinutes = 0;
        var nightStartMinutes = nightStart.Hour * 60 + nightStart.Minute;
        var nightEndMinutes = nightEnd.Hour * 60 + nightEnd.Minute;

        if (spansNextDay)
        {
            // Part 1: Start to midnight
            var startMinutes = startTime.Hour * 60 + startTime.Minute;
            if (startMinutes < nightStartMinutes)
                nightMinutes += 1440 - nightStartMinutes;
            else
                nightMinutes += 1440 - startMinutes;

            // Part 2: Midnight to end
            var endMinutes = endTime.Hour * 60 + endTime.Minute;
            nightMinutes += Math.Min(endMinutes, nightEndMinutes);
        }
        else
        {
            var startMinutes = startTime.Hour * 60 + startTime.Minute;
            var endMinutes = endTime.Hour * 60 + endTime.Minute;

            // Night spans midnight
            if (nightEndMinutes < nightStartMinutes)
            {
                // Evening part (nightStart to shift end or midnight)
                if (endMinutes >= nightStartMinutes)
                {
                    var nightPart = Math.Min(endMinutes, 1440) - Math.Max(startMinutes, nightStartMinutes);
                    if (nightPart > 0) nightMinutes += nightPart;
                }
                // Morning part (0 to nightEnd)
                if (startMinutes < nightEndMinutes)
                {
                    var nightPart = Math.Min(endMinutes, nightEndMinutes) - startMinutes;
                    if (nightPart > 0) nightMinutes += nightPart;
                }
            }
        }

        return Math.Max(0, nightMinutes / 60m);
    }

    private PayrollOptions GetPayrollOptions()
    {
        return _configuration.GetSection("Payroll").Get<PayrollOptions>() ?? new PayrollOptions();
    }

    private async Task<BordroOptions> GetBordroOptionsAsync(int organizationId, string? cadreType = null)
    {
        var options = _configuration.GetSection("Bordro").Get<BordroOptions>() ?? new BordroOptions();
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);

        var sabitler = await _context.BordroSabitleri
            .Where(s => s.OrganizationId == organizationId
                        && s.IsActive
                        && s.ValidFrom <= today
                        && (s.ValidTo == null || s.ValidTo >= today)
                        && (string.IsNullOrEmpty(s.CadreType) || s.CadreType == "GENEL" || s.CadreType == cadreType))
            .OrderByDescending(s => s.ValidFrom)
            .ToListAsync();

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

        return options;
    }

    private async Task<Dictionary<string, int>> GetPersonelPuanMapAsync(int organizationId)
    {
        return await _context.PersonelNobetPuan
            .Where(p => p.OrganizationId == organizationId && p.IsActive)
            .Where(p => !string.IsNullOrEmpty(p.TcKimlik))
            .ToDictionaryAsync(p => p.TcKimlik, p => p.YPuan);
    }

    private void AddBordro4ASheet(XLWorkbook workbook, List<Bordro4AResult> results, bool isTurkish)
    {
        var sheetName = isTurkish ? "4A Bordro" : "4A Payroll";
        var sheet = workbook.Worksheets.Add(sheetName);

        var headers = isTurkish
            ? new[]
            {
                "Personel", "Tip", "Saat Ücreti",
                "Normal Nöbet Saat", "YB Nöbet Saat",
                "Normal Bayram Saat", "YB Bayram Saat", "Bayram Farkı Saat",
                "Normal Nöbet Tutar", "YB Nöbet Tutar",
                "Normal Bayram Tutar", "YB Bayram Tutar", "Bayram Farkı Tutar",
                "Genel Toplam", "Damga Vergisi", "Net"
            }
            : new[]
            {
                "Employee", "Type", "Hourly Rate",
                "Normal OT Hours", "ICU OT Hours",
                "Normal Holiday Hours", "ICU Holiday Hours", "Holiday Diff Hours",
                "Normal OT Amount", "ICU OT Amount",
                "Normal Holiday Amount", "ICU Holiday Amount", "Holiday Diff Amount",
                "Gross Total", "Stamp Tax", "Net"
            };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        int row = 2;
        foreach (var item in results.OrderBy(r => r.EmployeeName))
        {
            sheet.Cell(row, 1).Value = item.EmployeeName;
            sheet.Cell(row, 2).Value = item.YogunBakimVar ? (isTurkish ? "Yoğun" : "ICU") : (isTurkish ? "Normal" : "Normal");
            sheet.Cell(row, 3).Value = (double)item.SaatUcreti;
            sheet.Cell(row, 4).Value = (double)item.NormalServisNobetSaati;
            sheet.Cell(row, 5).Value = (double)item.YogunBakimNobetSaati;
            sheet.Cell(row, 6).Value = (double)item.NormalServisBayramSaati;
            sheet.Cell(row, 7).Value = (double)item.YogunBakimBayramSaati;
            sheet.Cell(row, 8).Value = (double)item.BayramFarkiNobetSaati;
            sheet.Cell(row, 9).Value = (double)item.NormalServisNobetToplamTutar;
            sheet.Cell(row, 10).Value = (double)item.YogunBakimNobetToplamTutar;
            sheet.Cell(row, 11).Value = (double)item.NormalServisBayramToplamTutar;
            sheet.Cell(row, 12).Value = (double)item.YogunBakimBayramToplamTutar;
            sheet.Cell(row, 13).Value = (double)item.BayramFarkiToplamTutar;
            sheet.Cell(row, 14).Value = (double)item.GenelToplamTutar;
            sheet.Cell(row, 15).Value = (double)item.DamgaVergisi;
            sheet.Cell(row, 16).Value = (double)item.EleGecenToplam;

            for (int col = 3; col <= 16; col++)
            {
                sheet.Cell(row, col).Style.NumberFormat.Format = "0.00";
            }

            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private void AddBordro4BSheet(XLWorkbook workbook, List<Bordro4BResult> results, bool isTurkish)
    {
        var sheetName = isTurkish ? "4B Bordro" : "4B Payroll";
        var sheet = workbook.Worksheets.Add(sheetName);

        var headers = isTurkish
            ? new[]
            {
                "Personel", "Tip", "Saat Ücreti",
                "Normal Nöbet Saat", "YB Nöbet Saat",
                "Normal Bayram Saat", "YB Bayram Saat", "Bayram Farkı Saat",
                "Normal Nöbet Tutar", "YB Nöbet Tutar",
                "Normal Bayram Tutar", "YB Bayram Tutar", "Bayram Farkı Tutar",
                "PEK", "Maluliyet Dev", "GSS Dev", "Kısa Vadeli", "Gelir Toplam",
                "Damga", "Maluliyet Kişi", "GSS Kişi", "Kesinti Toplam", "Net"
            }
            : new[]
            {
                "Employee", "Type", "Hourly Rate",
                "Normal OT Hours", "ICU OT Hours",
                "Normal Holiday Hours", "ICU Holiday Hours", "Holiday Diff Hours",
                "Normal OT Amount", "ICU OT Amount",
                "Normal Holiday Amount", "ICU Holiday Amount", "Holiday Diff Amount",
                "PEK", "Disability Employer", "GSS Employer", "Short-Term", "Gross Income",
                "Stamp Tax", "Disability Employee", "GSS Employee", "Total Deduction", "Net"
            };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        int row = 2;
        foreach (var item in results.OrderBy(r => r.EmployeeName))
        {
            sheet.Cell(row, 1).Value = item.EmployeeName;
            sheet.Cell(row, 2).Value = item.YogunBakimVar ? (isTurkish ? "Yoğun" : "ICU") : (isTurkish ? "Normal" : "Normal");
            sheet.Cell(row, 3).Value = (double)item.SaatUcreti;
            sheet.Cell(row, 4).Value = (double)item.NormalServisNobetSaati;
            sheet.Cell(row, 5).Value = (double)item.YogunBakimNobetSaati;
            sheet.Cell(row, 6).Value = (double)item.NormalServisBayramSaati;
            sheet.Cell(row, 7).Value = (double)item.YogunBakimBayramSaati;
            sheet.Cell(row, 8).Value = (double)item.BayramFarkiNobetSaati;
            sheet.Cell(row, 9).Value = (double)item.NormalServisNobetToplamTutar;
            sheet.Cell(row, 10).Value = (double)item.YogunBakimNobetToplamTutar;
            sheet.Cell(row, 11).Value = (double)item.NormalServisBayramToplamTutar;
            sheet.Cell(row, 12).Value = (double)item.YogunBakimBayramToplamTutar;
            sheet.Cell(row, 13).Value = (double)item.BayramFarkiToplamTutar;
            sheet.Cell(row, 14).Value = (double)item.GenelToplamTutarPek;
            sheet.Cell(row, 15).Value = (double)item.MaluliyetYaslilikEmeklilikDev;
            sheet.Cell(row, 16).Value = (double)item.GssDev;
            sheet.Cell(row, 17).Value = (double)item.KisaVadSigKolPrim;
            sheet.Cell(row, 18).Value = (double)item.GelirToplami;
            sheet.Cell(row, 19).Value = (double)item.DamgaVergisi;
            sheet.Cell(row, 20).Value = (double)item.MaluliyetYaslilikEmeklilikKisi;
            sheet.Cell(row, 21).Value = (double)item.GssKisi;
            sheet.Cell(row, 22).Value = (double)item.KesintiToplami;
            sheet.Cell(row, 23).Value = (double)item.EleGecenToplam;

            for (int col = 3; col <= 23; col++)
            {
                sheet.Cell(row, col).Style.NumberFormat.Format = "0.00";
            }

            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private List<EmployeePayroll> CalculatePayrollFromAttendance(
        List<Employee> employees,
        List<TimeAttendance> attendances,
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
            var employeeAttendances = attendances.Where(a => a.EmployeeId == employee.Id).ToList();
            var employeeLeaves = leaves.Where(l => l.EmployeeId == employee.Id).ToList();
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

            foreach (var att in employeeAttendances.OrderBy(a => a.Date))
            {
                var holiday = holidays.FirstOrDefault(h => h.Date == att.Date);
                var isWeekend = weekendDays.Contains((int)att.Date.DayOfWeek);

                var detailIsIntensive = IsIntensiveCareByGroup(att.WorkGroupTypeId, att.IsRiskGroup, unit);
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
                    Note = att.Notes,
                    IsIntensiveCare = detailIsIntensive
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

                    if (att.CheckInTime.HasValue && att.CheckOutTime.HasValue)
                    {
                        var nightHours = CalculateFullNightHours(att.CheckInTime.Value, att.CheckOutTime.Value,
                            att.CheckOutToNextDay, nightStart, nightEnd, 0);
                        detail.NightHours = nightHours;
                        payroll.NightHours += nightHours;

                        var ticketCount = CalculateTicketCount(att.CheckInTime.Value, att.CheckOutTime.Value, employee.HasDoubleTicketRight);
                        payroll.TicketCount += ticketCount;
                        if (ticketCount > 0)
                            payroll.TransportationDays++;
                    }

                    var holidayHours = CalculateHolidayHoursForAttendance(att, holidays, year, month);
                    detail.HolidayHours = holidayHours;
                    payroll.HolidayHours += holidayHours;

                    var weekendHours = CalculateWeekendHoursForAttendance(att, weekendDays, holidays, year, month);
                    detail.WeekendHours = weekendHours;
                    payroll.WeekendHours += weekendHours;

                    var segments = GetAttendanceSegments(att)
                        .Where(s => s.Date.Year == year && s.Date.Month == month)
                        .ToList();
                    var grossTotal = segments.Sum(s => s.GrossHours);
                    var ratio = grossTotal > 0 ? att.WorkedHours.Value / grossTotal : 0;
                    foreach (var segment in segments)
                    {
                        payroll.CalculationSegments.Add(new WorkSegment
                        {
                            Date = segment.Date,
                            Hours = segment.GrossHours * ratio,
                            IsIntensiveCare = detailIsIntensive
                        });
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
                        payroll.CalculationSegments.Add(new WorkSegment
                        {
                            Date = segment.Date,
                            Hours = segment.Hours,
                            IsIntensiveCare = detailIsIntensive
                        });
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
        decimal requiredHours = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var dayOfWeek = date.DayOfWeek;
            var isSaturday = dayOfWeek == DayOfWeek.Saturday;
            var isWeekend = weekendDays.Contains((int)dayOfWeek);

            if (leaves != null && leaves.Any(l => l.Date == date))
                continue;

            var holiday = holidays.FirstOrDefault(h => h.Date == date);

            if (holiday != null && !holiday.IsHalfDay)
                continue;

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
                    case 1:
                        requiredHours += employee.DailyWorkHours;
                        break;
                    case 2:
                        if (isSaturday) requiredHours += employee.DailyWorkHours;
                        break;
                    case 3:
                        if (isSaturday && employee.SaturdayWorkHours.HasValue)
                            requiredHours += employee.SaturdayWorkHours.Value;
                        break;
                    default:
                        if (isSaturday && employee.PositionType?.Equals("4D", StringComparison.OrdinalIgnoreCase) == true)
                            requiredHours += 5;
                        break;
                }
            }
            else
            {
                if (isRiskGroup)
                    requiredHours += 7;
                else
                    requiredHours += employee.DailyWorkHours;
            }
        }

        return Math.Max(0, requiredHours);
    }

    private decimal CalculateRequiredHoursForDate(Employee employee, DateOnly date, List<Holiday> holidays, List<int> weekendDays, bool isRiskGroup, List<Leave>? leaves = null)
    {
        if (leaves != null && leaves.Any(l => l.Date == date))
            return 0;

        var dayOfWeek = date.DayOfWeek;
        var isSaturday = dayOfWeek == DayOfWeek.Saturday;
        var isWeekend = weekendDays.Contains((int)dayOfWeek);
        var holiday = holidays.FirstOrDefault(h => h.Date == date);

        if (holiday != null && !holiday.IsHalfDay)
            return 0;

        if (holiday != null && holiday.IsHalfDay)
        {
            if (!isWeekend || employee.WeekendWorkMode > 0)
            {
                return holiday.HalfDayWorkHours ?? 4;
            }
            return 0;
        }

        if (isWeekend)
        {
            switch (employee.WeekendWorkMode)
            {
                case 1:
                    return employee.DailyWorkHours;
                case 2:
                    return isSaturday ? employee.DailyWorkHours : 0;
                case 3:
                    return isSaturday && employee.SaturdayWorkHours.HasValue
                        ? employee.SaturdayWorkHours.Value
                        : 0;
                default:
                    if (isSaturday && employee.PositionType?.Equals("4D", StringComparison.OrdinalIgnoreCase) == true)
                        return 5;
                    return 0;
            }
        }

        if (isRiskGroup)
            return 7;

        return employee.DailyWorkHours;
    }

    private (decimal total, decimal normal, decimal intensive) CalculateDailyOvertimeHours(
        Employee employee,
        List<ShiftDetail> shiftDetails,
        List<WorkSegment> calculationSegments,
        List<Holiday> holidays,
        List<int> weekendDays,
        bool isRiskGroup,
        List<Leave>? leaves)
    {
        decimal totalOvertime = 0;
        decimal normalOvertime = 0;
        decimal intensiveOvertime = 0;

        var grouped = (calculationSegments.Any()
                ? calculationSegments.Where(s => s.Hours > 0)
                    .Select(s => new { s.Date, s.Hours, s.IsIntensiveCare })
                : shiftDetails.Where(d => d.TotalHours > 0)
                    .Select(d => new { d.Date, Hours = d.TotalHours, d.IsIntensiveCare }))
            .GroupBy(d => d.Date);

        foreach (var group in grouped)
        {
            var totalHours = group.Sum(d => d.Hours);
            var required = CalculateRequiredHoursForDate(employee, group.Key, holidays, weekendDays, isRiskGroup, leaves);
            var overtimeForDay = Math.Max(0, totalHours - required);

            foreach (var detail in group)
            {
                var share = totalHours > 0 ? detail.Hours / totalHours : 0;
                var detailOvertime = overtimeForDay * share;

                totalOvertime += detailOvertime;
                if (detail.IsIntensiveCare)
                {
                    intensiveOvertime += detailOvertime;
                }
                else
                {
                    normalOvertime += detailOvertime;
                }
            }
        }

        return (totalOvertime, normalOvertime, intensiveOvertime);
    }

    private void FinalizePayrollTotals(
        EmployeePayroll payroll,
        Employee employee,
        Organization organization,
        List<Holiday> holidays,
        List<int> weekendDays,
        bool isRiskGroup,
        List<Leave> employeeLeaves,
        PayrollOptions payrollOptions)
    {
        decimal rawOvertime;
        decimal normalOvertime;
        decimal intensiveOvertime;

        if (organization.OvertimeCalcMode == OvertimeCalcMode.Daily)
        {
            var overtime = CalculateDailyOvertimeHours(employee, payroll.ShiftDetails, payroll.CalculationSegments, holidays, weekendDays, isRiskGroup, employeeLeaves);
            rawOvertime = overtime.total;
            normalOvertime = overtime.normal;
            intensiveOvertime = overtime.intensive;
        }
        else
        {
            rawOvertime = Math.Max(0, payroll.TotalWorkedHours - payroll.RequiredHours);
            if (payroll.IsIntensiveCare)
            {
                normalOvertime = 0;
                intensiveOvertime = rawOvertime;
            }
            else
            {
                normalOvertime = rawOvertime;
                intensiveOvertime = 0;
            }
        }

        payroll.RawOvertimeHours = rawOvertime;
        payroll.NormalOvertimeHours = normalOvertime;
        payroll.IntensiveOvertimeHours = intensiveOvertime;

        payroll.NormalHolidayHours = payroll.ShiftDetails.Where(d => !d.IsIntensiveCare).Sum(d => d.HolidayHours);
        payroll.IntensiveHolidayHours = payroll.ShiftDetails.Where(d => d.IsIntensiveCare).Sum(d => d.HolidayHours);

        var holidayWork = payroll.HolidayHours;
        payroll.HolidayOvertimeHours = Math.Min(rawOvertime, holidayWork);
        payroll.HolidayDifferenceHours = Math.Max(0, holidayWork - rawOvertime);

        if (holidayWork > 0)
        {
            var newTotalOvertime = Math.Max(0, rawOvertime - holidayWork);
            if (rawOvertime > 0)
            {
                var normalRatio = normalOvertime / rawOvertime;
                var intensiveRatio = intensiveOvertime / rawOvertime;
                normalOvertime = newTotalOvertime * normalRatio;
                intensiveOvertime = newTotalOvertime * intensiveRatio;
            }
            else
            {
                normalOvertime = 0;
                intensiveOvertime = 0;
            }
        }

        if (payrollOptions.OvertimeLimitHours > 0)
        {
            (normalOvertime, intensiveOvertime) = LimitOvertimeHours(normalOvertime, intensiveOvertime, payrollOptions.OvertimeLimitHours);
        }

        payroll.NormalOvertimeHours = normalOvertime;
        payroll.IntensiveOvertimeHours = intensiveOvertime;
        payroll.OvertimeHours = normalOvertime + intensiveOvertime;
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
        {
            ticketCount++;
        }
        if (end < new TimeOnly(16, 0))
        {
            ticketCount++;
        }
        if (start < new TimeOnly(8, 0) && end > new TimeOnly(16, 0))
        {
            ticketCount++;
        }
        if (end > new TimeOnly(20, 0))
        {
            ticketCount++;
        }
        if (start >= new TimeOnly(16, 0))
        {
            ticketCount++;
        }

        if (hasDoubleTicketRight)
        {
            ticketCount += ticketCount;
            if (start == new TimeOnly(8, 0) && end.ToTimeSpan() >= TimeSpan.FromHours(16.5) && end.ToTimeSpan() <= TimeSpan.FromHours(17))
            {
                ticketCount = Math.Max(2, ticketCount);
            }
        }

        return ticketCount;
    }

    private decimal CalculateHolidayHoursForShift(Shift shift, Employee employee, List<Holiday> holidays, int year, int month)
    {
        if (shift.IsDayOff)
            return 0;

        var breakMinutes = 0;
        var segments = GetShiftDaySegments(shift);
        if (shift.SpansNextDay && shift.OvernightHoursMode == 0)
        {
            segments = segments.Where(s => s.Date.Year == year && s.Date.Month == month).ToList();
        }
        var totalGross = segments.Sum(s => s.GrossHours);
        decimal holidayHours = 0;

        foreach (var segment in segments)
        {
            var holiday = holidays.FirstOrDefault(h => h.Date == segment.Date);
            if (holiday == null)
                continue;

            if (!holiday.IsHalfDay)
            {
                if (totalGross > 0 && breakMinutes > 0)
                {
                    var proportionalBreak = (breakMinutes / 60m) * (segment.GrossHours / totalGross);
                    holidayHours += Math.Max(0, segment.GrossHours - proportionalBreak);
                }
                else
                {
                    holidayHours += segment.GrossHours;
                }
            }
            else
            {
                holidayHours += CalculateHoursAfterThreshold(segment.StartMinutes, segment.EndMinutes, 13 * 60);
            }
        }

        return Math.Max(0, holidayHours);
    }

    private decimal CalculateHolidayHoursForAttendance(TimeAttendance attendance, List<Holiday> holidays, int year, int month)
    {
        if (!attendance.WorkedHours.HasValue || attendance.WorkedHours.Value <= 0)
            return 0;

        if (!attendance.CheckInTime.HasValue || !attendance.CheckOutTime.HasValue)
        {
            var holidayFallback = holidays.FirstOrDefault(h => h.Date == attendance.Date);
            if (holidayFallback == null)
                return 0;

            if (holidayFallback.IsHalfDay)
                return 0;

            return attendance.WorkedHours.Value;
        }

        var segments = GetAttendanceSegments(attendance)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .ToList();
        var grossTotal = segments.Sum(s => s.GrossHours);
        var ratio = grossTotal > 0 ? attendance.WorkedHours.Value / grossTotal : 0;
        decimal holidayHours = 0;

        foreach (var segment in segments)
        {
            var holiday = holidays.FirstOrDefault(h => h.Date == segment.Date);
            if (holiday == null)
                continue;

            if (!holiday.IsHalfDay)
            {
                holidayHours += segment.GrossHours * ratio;
            }
            else
            {
                holidayHours += CalculateHoursAfterThreshold(segment.StartMinutes, segment.EndMinutes, 13 * 60) * ratio;
            }
        }

        return Math.Max(0, holidayHours);
    }

    private decimal CalculateWeekendHoursForShift(Shift shift, Employee employee, List<int> weekendDays, List<Holiday> holidays, int year, int month)
    {
        if (shift.IsDayOff)
            return 0;

        var breakMinutes = GetEffectiveBreakMinutes(shift, employee);
        var segments = GetShiftDaySegments(shift);
        if (shift.SpansNextDay && shift.OvernightHoursMode == 0)
        {
            segments = segments.Where(s => s.Date.Year == year && s.Date.Month == month).ToList();
        }
        decimal weekendHours = 0;

        foreach (var segment in segments)
        {
            if (!weekendDays.Contains((int)segment.Date.DayOfWeek))
                continue;

            if (holidays.Any(h => h.Date == segment.Date))
                continue;

            var netHours = segment.GrossHours;
            if (segment.IsFirstDay && breakMinutes > 0)
            {
                netHours = Math.Max(0, netHours - breakMinutes / 60m);
            }

            weekendHours += netHours;
        }

        return Math.Max(0, weekendHours);
    }

    private decimal CalculateWeekendHoursForAttendance(TimeAttendance attendance, List<int> weekendDays, List<Holiday> holidays, int year, int month)
    {
        if (!attendance.WorkedHours.HasValue || attendance.WorkedHours.Value <= 0)
            return 0;

        var segments = GetAttendanceSegments(attendance)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .ToList();
        var grossTotal = segments.Sum(s => s.GrossHours);
        var ratio = grossTotal > 0 ? attendance.WorkedHours.Value / grossTotal : 0;
        decimal weekendHours = 0;

        foreach (var segment in segments)
        {
            if (!weekendDays.Contains((int)segment.Date.DayOfWeek))
                continue;

            if (holidays.Any(h => h.Date == segment.Date))
                continue;

            weekendHours += segment.GrossHours * ratio;
        }

        return Math.Max(0, weekendHours);
    }

    private decimal CalculateHoursAfterThreshold(int startMinutes, int endMinutes, int thresholdMinutes)
    {
        if (endMinutes <= thresholdMinutes)
            return 0;

        var effectiveStart = Math.Max(startMinutes, thresholdMinutes);
        return Math.Max(0, (endMinutes - effectiveStart) / 60m);
    }

    private int GetEffectiveBreakMinutes(Shift shift, Employee employee)
    {
        var grossHours = GetShiftGrossMinutes(shift) / 60m;
        var is24HourShift = grossHours >= 23.5m;
        var is4D = employee.PositionType?.Equals("4D", StringComparison.OrdinalIgnoreCase) == true;

        if (is24HourShift && !is4D)
            return 0;

        return shift.BreakMinutes;
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

        foreach (var segment in segments)
        {
            var netHours = segment.GrossHours;
            if (segment.IsFirstDay && breakMinutes > 0)
            {
                netHours = Math.Max(0, netHours - breakMinutes / 60m);
            }

            results.Add(new WorkSegment
            {
                Date = segment.Date,
                Hours = netHours
            });
        }

        return results;
    }

    private List<ShiftDaySegment> GetAttendanceSegments(TimeAttendance attendance)
    {
        var segments = new List<ShiftDaySegment>();
        if (!attendance.CheckInTime.HasValue || !attendance.CheckOutTime.HasValue)
            return segments;

        if (!attendance.CheckOutToNextDay)
        {
            var start = attendance.CheckInTime.Value;
            var end = attendance.CheckOutTime.Value;
            var startMinutes = start.Hour * 60 + start.Minute;
            var endMinutes = end.Hour * 60 + end.Minute;
            var gross = Math.Max(0, endMinutes - startMinutes) / 60m;
            segments.Add(new ShiftDaySegment(attendance.Date, startMinutes, endMinutes, gross, true));
            return segments;
        }

        var dayOneMinutes = 1440 - (attendance.CheckInTime.Value.Hour * 60 + attendance.CheckInTime.Value.Minute);
        var dayTwoMinutes = attendance.CheckOutTime.Value.Hour * 60 + attendance.CheckOutTime.Value.Minute;

        segments.Add(new ShiftDaySegment(
            attendance.Date,
            attendance.CheckInTime.Value.Hour * 60 + attendance.CheckInTime.Value.Minute,
            1440,
            Math.Max(0, dayOneMinutes / 60m),
            true));

        segments.Add(new ShiftDaySegment(
            attendance.Date.AddDays(1),
            0,
            attendance.CheckOutTime.Value.Hour * 60 + attendance.CheckOutTime.Value.Minute,
            Math.Max(0, dayTwoMinutes / 60m),
            false));

        return segments;
    }

    private static (decimal normal, decimal intensive) LimitOvertimeHours(decimal normal, decimal intensive, decimal maxHours)
    {
        var total = normal + intensive;
        if (total <= maxHours)
            return (normal, intensive);

        if (intensive >= maxHours)
            return (0, maxHours);

        var remaining = Math.Max(0, maxHours - intensive);
        return (remaining, intensive);
    }

    private sealed record ShiftDaySegment(DateOnly Date, int StartMinutes, int EndMinutes, decimal GrossHours, bool IsFirstDay);

    private decimal CalculateNightHours(Shift shift, Employee employee, TimeOnly nightStart, TimeOnly nightEnd, int year, int month)
    {
        if (shift.IsDayOff) return 0;

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lastDayOfMonth = new DateOnly(year, month, daysInMonth);
        bool spansToNextMonth = shift.SpansNextDay && shift.Date == lastDayOfMonth;
        var breakMinutes = GetEffectiveBreakMinutes(shift, employee);

        if (spansToNextMonth && shift.OvernightHoursMode == 0)
        {
            return CalculateNightHoursBeforeMidnight(shift, nightStart, nightEnd);
        }

        return CalculateFullNightHours(shift.StartTime, shift.EndTime, shift.SpansNextDay, nightStart, nightEnd, breakMinutes);
    }

    private decimal CalculateFullNightHours(TimeOnly start, TimeOnly end, bool spansNextDay, TimeOnly nightStart, TimeOnly nightEnd, int breakMinutes)
    {
        decimal nightMinutes = 0;

        if (spansNextDay)
        {
            nightMinutes += GetNightMinutesInRange(start, new TimeOnly(23, 59, 59), nightStart, nightEnd);
            nightMinutes += GetNightMinutesInRange(new TimeOnly(0, 0), end, nightStart, nightEnd);
        }
        else
        {
            nightMinutes += GetNightMinutesInRange(start, end, nightStart, nightEnd);
        }

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

    private decimal GetNightMinutesInRange(TimeOnly start, TimeOnly end, TimeOnly nightStart, TimeOnly nightEnd)
    {
        decimal nightMinutes = 0;

        var startMinutes = start.Hour * 60 + start.Minute;
        var endMinutes = end.Hour * 60 + end.Minute;
        var nightStartMinutes = nightStart.Hour * 60 + nightStart.Minute;
        var nightEndMinutes = nightEnd.Hour * 60 + nightEnd.Minute;

        if (nightEndMinutes < nightStartMinutes)
        {
            if (endMinutes >= nightStartMinutes || startMinutes >= nightStartMinutes)
            {
                var periodStart = Math.Max(startMinutes, nightStartMinutes);
                var periodEnd = endMinutes >= nightStartMinutes ? Math.Min(endMinutes, 1440) : 1440;
                if (periodEnd > periodStart)
                    nightMinutes += periodEnd - periodStart;
            }

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
            var overlapStart = Math.Max(startMinutes, nightStartMinutes);
            var overlapEnd = Math.Min(endMinutes, nightEndMinutes);
            if (overlapEnd > overlapStart)
                nightMinutes += overlapEnd - overlapStart;
        }

        return nightMinutes;
    }

    private decimal CalculateNightHoursBeforeMidnight(Shift shift, TimeOnly nightStart, TimeOnly nightEnd)
    {
        var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
        var nightStartMinutes = nightStart.Hour * 60 + nightStart.Minute;

        if (startMinutes < nightStartMinutes)
        {
            return (1440 - nightStartMinutes) / 60m;
        }
        else
        {
            return (1440 - startMinutes) / 60m;
        }
    }

    private decimal CalculateNightHoursAfterMidnight(Shift shift, TimeOnly nightStart, TimeOnly nightEnd)
    {
        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
        var nightEndMinutes = nightEnd.Hour * 60 + nightEnd.Minute;

        return Math.Min(endMinutes, nightEndMinutes) / 60m;
    }

    private decimal CalculateHoursAfterMidnight(Shift shift, Employee employee)
    {
        var endMinutes = shift.EndTime.Hour * 60 + shift.EndTime.Minute;
        return Math.Max(0, endMinutes / 60m);
    }

    private decimal CalculateShiftHoursForPayroll(Shift shift, Employee employee, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var lastDayOfMonth = new DateOnly(year, month, daysInMonth);
        var breakMinutes = GetEffectiveBreakMinutes(shift, employee);

        if (!shift.SpansNextDay || shift.Date != lastDayOfMonth)
            return CalculateNetShiftHours(shift, breakMinutes);

        if (shift.OvernightHoursMode == 0)
        {
            var startMinutes = shift.StartTime.Hour * 60 + shift.StartTime.Minute;
            var minutesUntilMidnight = 1440 - startMinutes;
            var breakBeforeMidnight = Math.Min(breakMinutes, minutesUntilMidnight);

            return Math.Max(0, (minutesUntilMidnight - breakBeforeMidnight) / 60m);
        }

        return CalculateNetShiftHours(shift, breakMinutes);
    }
}
