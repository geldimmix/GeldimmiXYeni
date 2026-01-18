using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Net.Http.Headers;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Resources;
using System.Globalization;
using System.Text;

namespace Nobetci.Web.Controllers;

[Route("api/export")]
public class ExportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ExportController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _userManager = userManager;
        _localizer = localizer;
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

        // Get current culture for localization
        var culture = CultureInfo.CurrentUICulture;
        var isTurkish = culture.TwoLetterISOLanguageName == "tr";

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

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
        
        // Get localized month name
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

        var culture = CultureInfo.CurrentUICulture;
        var isTurkish = culture.TwoLetterISOLanguageName == "tr";

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organization.Id && e.IsActive)
            .OrderBy(e => e.FullName)
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

        var monthDate = new DateTime(year, month, 1);
        var monthName = monthDate.ToString("MMMM yyyy", culture);

        // Sheet name
        var sourceText = source == "attendance" 
            ? (isTurkish ? "Mesai Takip" : "Attendance") 
            : (isTurkish ? "Nöbet" : "Shift");
        var sheetName = isTurkish ? $"Puantaj - {monthName}" : $"Payroll - {monthName}";

        var headers = isTurkish
            ? new[] { "Personel", "Ünvan", "Çalışılan Gün", "Çalışılan Saat", "Hedef Saat", "Fazla Mesai", "Gece Çalışma", "Hafta Sonu", "Resmi Tatil", "İzin Günü" }
            : new[] { "Employee", "Title", "Days Worked", "Hours Worked", "Target Hours", "Overtime", "Night Hours", "Weekend Hours", "Holiday Hours", "Days Off" };

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
        foreach (var employee in employees)
        {
            decimal workedHours, nightHours, weekendHours, holidayHours, requiredHours;
            int workedDays, daysOff;

            // Calculate required hours for employee
            requiredHours = CalculateRequiredHoursForExport(employee, year, month, holidays, weekendDays);

            if (source == "attendance")
            {
                // Calculate from attendance
                var empAttendances = attendances.Where(a => a.EmployeeId == employee.Id).ToList();
                workedDays = empAttendances.Count(a => a.WorkedHours > 0);
                workedHours = empAttendances.Where(a => a.WorkedHours.HasValue).Sum(a => a.WorkedHours ?? 0);
                weekendHours = empAttendances
                    .Where(a => a.WorkedHours > 0 && weekendDays.Contains((int)a.Date.DayOfWeek))
                    .Sum(a => a.WorkedHours ?? 0);
                holidayHours = empAttendances
                    .Where(a => a.WorkedHours > 0 && holidays.Any(h => h.Date == a.Date))
                    .Sum(a => a.WorkedHours ?? 0);
                daysOff = empAttendances.Count(a => a.Type == AttendanceType.DayOff);
                
                // Calculate night hours from attendance
                nightHours = 0;
                foreach (var att in empAttendances.Where(a => a.CheckInTime.HasValue && a.CheckOutTime.HasValue))
                {
                    nightHours += CalculateNightHoursFromTimes(att.CheckInTime!.Value, att.CheckOutTime!.Value, 
                        att.CheckOutToNextDay, nightStart, nightEnd);
                }
            }
            else
            {
                // Calculate from shifts
                var employeeShifts = shifts.Where(s => s.EmployeeId == employee.Id).ToList();
                var prevMonthShift = prevMonthOvernightShifts.FirstOrDefault(s => s.EmployeeId == employee.Id);
                
                workedDays = employeeShifts.Count(s => !s.IsDayOff);
                workedHours = CalculateWorkedHoursForExport(employeeShifts, prevMonthShift, year, month);
                nightHours = CalculateNightHoursForExport(employeeShifts, prevMonthShift, nightStart, nightEnd, year, month);
                weekendHours = employeeShifts.Where(s => !s.IsDayOff && weekendDays.Contains((int)s.Date.DayOfWeek)).Sum(s => s.TotalHours);
                holidayHours = employeeShifts.Where(s => !s.IsDayOff && holidays.Any(h => h.Date == s.Date)).Sum(s => s.TotalHours);
                daysOff = employeeShifts.Count(s => s.IsDayOff);
            }

            var overtime = Math.Max(0, workedHours - requiredHours);

            worksheet.Cell(row, 1).Value = employee.FullName;
            worksheet.Cell(row, 2).Value = employee.Title ?? "";
            worksheet.Cell(row, 3).Value = workedDays;
            worksheet.Cell(row, 4).Value = (double)workedHours;
            worksheet.Cell(row, 5).Value = (double)requiredHours;
            worksheet.Cell(row, 6).Value = (double)overtime;
            worksheet.Cell(row, 7).Value = (double)nightHours;
            worksheet.Cell(row, 8).Value = (double)weekendHours;
            worksheet.Cell(row, 9).Value = (double)holidayHours;
            worksheet.Cell(row, 10).Value = daysOff;

            // Format numbers
            for (int col = 4; col <= 9; col++)
            {
                worksheet.Cell(row, col).Style.NumberFormat.Format = "0.0";
            }
            
            // Highlight overtime in green if positive
            if (overtime > 0)
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

        var savedPayroll = await _context.SavedPayrolls
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organization.Id);

        if (savedPayroll == null)
            return NotFound();

        var culture = CultureInfo.CurrentUICulture;
        var isTurkish = culture.TwoLetterISOLanguageName == "tr";

        // Parse the saved payroll data
        var entries = System.Text.Json.JsonSerializer.Deserialize<List<SavedPayrollEntry>>(savedPayroll.PayrollDataJson) 
            ?? new List<SavedPayrollEntry>();

        var monthDate = new DateTime(savedPayroll.Year, savedPayroll.Month, 1);
        var monthName = monthDate.ToString("MMMM yyyy", culture);

        var sourceText = savedPayroll.DataSource == "attendance" 
            ? (isTurkish ? "Mesai Takip" : "Attendance") 
            : (isTurkish ? "Nöbet" : "Shift");
        var summarySheetName = isTurkish ? "Özet" : "Summary";
        var detailSheetName = isTurkish ? "Detay" : "Details";

        var headers = isTurkish
            ? new[] { "Personel", "Ünvan", "Çalışılan Gün", "Çalışılan Saat", "Hedef Saat", "Fazla Mesai", "Gece Çalışma", "Hafta Sonu", "Resmi Tatil", "İzin Günü" }
            : new[] { "Employee", "Title", "Days Worked", "Hours Worked", "Target Hours", "Overtime", "Night Hours", "Weekend Hours", "Holiday Hours", "Days Off" };

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
        foreach (var entry in entries.OrderBy(e => e.EmployeeName))
        {
            summarySheet.Cell(row, 1).Value = entry.EmployeeName;
            summarySheet.Cell(row, 2).Value = entry.EmployeeTitle ?? "";
            summarySheet.Cell(row, 3).Value = entry.WorkedDays;
            summarySheet.Cell(row, 4).Value = (double)entry.TotalWorkedHours;
            summarySheet.Cell(row, 5).Value = (double)entry.RequiredHours;
            summarySheet.Cell(row, 6).Value = (double)entry.OvertimeHours;
            summarySheet.Cell(row, 7).Value = (double)entry.NightHours;
            summarySheet.Cell(row, 8).Value = (double)entry.WeekendHours;
            summarySheet.Cell(row, 9).Value = (double)entry.HolidayHours;
            summarySheet.Cell(row, 10).Value = entry.DayOffCount;

            // Format numbers
            for (int col = 4; col <= 9; col++)
            {
                summarySheet.Cell(row, col).Style.NumberFormat.Format = "0.0";
            }
            
            // Highlight overtime in green
            if (entry.OvertimeHours > 0)
            {
                summarySheet.Cell(row, 6).Style.Font.FontColor = XLColor.Green;
                summarySheet.Cell(row, 6).Style.Font.Bold = true;
            }

            row++;
        }

        // Auto-fit columns
        summarySheet.Columns().AdjustToContents();

        // Add borders
        if (entries.Any())
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
}
