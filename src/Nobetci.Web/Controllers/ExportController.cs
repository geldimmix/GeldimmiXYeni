using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Resources;
using System.Globalization;

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
            
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
}
