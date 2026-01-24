using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

public class AttendanceViewModel
{
    public Organization Organization { get; set; } = null!;
    public List<Employee> Employees { get; set; } = new();
    public List<TimeAttendance> Attendances { get; set; } = new();
    public List<Shift> Shifts { get; set; } = new();
    public List<Holiday> Holidays { get; set; } = new();
    
    // Unit filtering (Premium feature)
    public List<Unit> Units { get; set; } = new();
    public int? SelectedUnitId { get; set; }
    public bool IsPremium { get; set; }
    
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    
    // Helper properties
    public int DaysInMonth => DateTime.DaysInMonth(SelectedYear, SelectedMonth);
    public string MonthName => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");
    public DateOnly FirstDayOfMonth => new DateOnly(SelectedYear, SelectedMonth, 1);
    public DateOnly LastDayOfMonth => new DateOnly(SelectedYear, SelectedMonth, DaysInMonth);
    
    public TimeAttendance? GetAttendance(int employeeId, DateOnly date)
    {
        return Attendances.FirstOrDefault(a => a.EmployeeId == employeeId && a.Date == date);
    }
    
    public Shift? GetShift(int employeeId, DateOnly date)
    {
        return Shifts.FirstOrDefault(s => s.EmployeeId == employeeId && s.Date == date);
    }
    
    public bool IsHoliday(DateOnly date)
    {
        return Holidays.Any(h => h.Date == date);
    }
    
    public bool IsWeekend(DateOnly date)
    {
        var weekendDays = Organization.WeekendDays.Split(',').Select(int.Parse).ToList();
        return weekendDays.Contains((int)date.DayOfWeek);
    }
}

public class AttendanceSummary
{
    public Employee Employee { get; set; } = null!;
    public int TotalDays { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int LateDays { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal ExpectedHours { get; set; }
}

