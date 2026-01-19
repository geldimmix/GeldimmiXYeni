using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

public class AppViewModel
{
    public Organization Organization { get; set; } = null!;
    public List<Employee> Employees { get; set; } = new();
    public List<ShiftTemplate> ShiftTemplates { get; set; } = new();
    public List<Holiday> Holidays { get; set; } = new();
    public List<Shift> Shifts { get; set; } = new();
    public List<Shift> PreviousMonthOvernightShifts { get; set; } = new();
    public List<LeaveType> LeaveTypes { get; set; } = new();
    public List<Leave> Leaves { get; set; } = new();
    
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    
    public int EmployeeLimit { get; set; }
    public bool IsRegistered { get; set; }
    
    // Premium features - only for registered users
    public bool CanUseSmartScheduling { get; set; }
    public bool CanUseTimesheet { get; set; }
    public bool CanExportExcel { get; set; }
    
    // Helper properties
    public int EmployeeCount => Employees.Count;
    public bool CanAddEmployee => EmployeeCount < EmployeeLimit;
    public int DaysInMonth => DateTime.DaysInMonth(SelectedYear, SelectedMonth);
    
    public DateOnly FirstDayOfMonth => new DateOnly(SelectedYear, SelectedMonth, 1);
    public DateOnly LastDayOfMonth => new DateOnly(SelectedYear, SelectedMonth, DaysInMonth);
    
    public string MonthName => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");
    
    public List<Shift> GetShiftsForEmployee(int employeeId)
    {
        return Shifts.Where(s => s.EmployeeId == employeeId).ToList();
    }
    
    public Shift? GetShiftForEmployeeOnDate(int employeeId, DateOnly date)
    {
        return Shifts.FirstOrDefault(s => s.EmployeeId == employeeId && s.Date == date);
    }
    
    // Get overnight shift from previous month that continues into the first day
    public Shift? GetPreviousMonthOvernightShift(int employeeId)
    {
        return PreviousMonthOvernightShifts.FirstOrDefault(s => s.EmployeeId == employeeId);
    }
    
    public bool IsHoliday(DateOnly date)
    {
        return Holidays.Any(h => h.Date == date);
    }
    
    public Holiday? GetHoliday(DateOnly date)
    {
        return Holidays.FirstOrDefault(h => h.Date == date);
    }
    
    public bool IsWeekend(DateOnly date)
    {
        var weekendDays = Organization.WeekendDays.Split(',').Select(int.Parse).ToList();
        return weekendDays.Contains((int)date.DayOfWeek);
    }
    
    public Leave? GetLeaveForEmployeeOnDate(int employeeId, DateOnly date)
    {
        return Leaves.FirstOrDefault(l => l.EmployeeId == employeeId && l.Date == date);
    }
    
    public bool HasLeave(int employeeId, DateOnly date)
    {
        return Leaves.Any(l => l.EmployeeId == employeeId && l.Date == date);
    }
}

