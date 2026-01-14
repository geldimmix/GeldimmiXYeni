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

        var shifts = await _context.Shifts
            .Include(s => s.Employee)
            .Where(s => s.Employee.OrganizationId == organization.Id)
            .Where(s => s.Date.Year == year && s.Date.Month == month)
            .ToListAsync();

        var holidays = await _context.Holidays
            .Where(h => h.OrganizationId == organization.Id)
            .Where(h => h.Date.Year == year && h.Date.Month == month)
            .ToListAsync();

        var daysInMonth = DateTime.DaysInMonth(year, month);
        
        // Get localized month name
        var monthDate = new DateTime(year, month, 1);
        var monthName = monthDate.ToString("MMMM yyyy", culture);

        // Localized texts
        var sheetName = isTurkish ? $"Nöbet Listesi - {monthName}" : $"Shift Schedule - {monthName}";
        var employeeHeader = isTurkish ? "Personel" : "Employee";
        var totalHoursHeader = isTurkish ? "Toplam Saat" : "Total Hours";
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

            decimal totalHours = 0;

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
                        
                        // Add hours in parentheses
                        var hoursText = shift.TotalHours % 1 == 0 
                            ? $"({(int)shift.TotalHours}{hoursAbbrev})"
                            : $"({shift.TotalHours:0.#}{hoursAbbrev})";
                        
                        cell.Value = $"{timeText}\n{hoursText}";
                        cell.Style.Font.FontSize = 9;
                        cell.Style.Alignment.WrapText = true;
                        totalHours += shift.TotalHours;
                    }
                }
            }

            // Total hours for employee
            worksheet.Cell(row, daysInMonth + 2).Value = totalHours;
            worksheet.Cell(row, daysInMonth + 2).Style.Font.Bold = true;
            worksheet.Cell(row, daysInMonth + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(row, daysInMonth + 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            row++;
        }

        // Set row heights for better display
        worksheet.Row(1).Height = 35;
        for (int r = 2; r < row; r++)
        {
            worksheet.Row(r).Height = 30;
        }

        // Auto-fit columns
        worksheet.Column(1).Width = 22;
        for (int col = 2; col <= daysInMonth + 1; col++)
        {
            worksheet.Column(col).Width = 12;
        }
        worksheet.Column(daysInMonth + 2).Width = 12;

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
}
