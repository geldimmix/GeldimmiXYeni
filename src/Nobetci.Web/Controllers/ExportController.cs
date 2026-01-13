using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Resources;

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
        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add($"Nöbet Listesi - {monthName}");

        // Header row
        worksheet.Cell(1, 1).Value = _localizer["Employees"].Value;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightGray;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var holiday = holidays.FirstOrDefault(h => h.Date == date);
            var isWeekend = IsWeekend(date, organization);

            var cell = worksheet.Cell(1, day + 1);
            cell.Value = day;
            cell.Style.Font.Bold = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

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
        worksheet.Cell(1, daysInMonth + 2).Value = _localizer["TotalHours"].Value;
        worksheet.Cell(1, daysInMonth + 2).Style.Font.Bold = true;
        worksheet.Cell(1, daysInMonth + 2).Style.Fill.BackgroundColor = XLColor.LightGray;

        // Employee rows
        int row = 2;
        foreach (var employee in employees)
        {
            worksheet.Cell(row, 1).Value = employee.FullName;
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
                    var shiftText = $"{shift.StartTime:HH:mm}-{shift.EndTime:HH:mm}";
                    if (shift.SpansNextDay)
                    {
                        shiftText += "↓";
                    }
                    cell.Value = shiftText;
                    cell.Style.Font.FontSize = 9;
                    totalHours += shift.TotalHours;
                }
            }

            worksheet.Cell(row, daysInMonth + 2).Value = totalHours;
            worksheet.Cell(row, daysInMonth + 2).Style.Font.Bold = true;
            worksheet.Cell(row, daysInMonth + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;
        }

        // Auto-fit columns
        worksheet.Column(1).Width = 20;
        for (int col = 2; col <= daysInMonth + 2; col++)
        {
            worksheet.Column(col).Width = 10;
        }

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

        var fileName = $"Nobet_Listesi_{year}_{month:00}.xlsx";
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

