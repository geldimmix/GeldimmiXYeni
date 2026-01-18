using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

public class PayrollViewModel
{
    public Organization Organization { get; set; } = null!;
    public List<Employee> Employees { get; set; } = new();
    public List<Holiday> Holidays { get; set; } = new();
    public List<Shift> Shifts { get; set; } = new();
    public List<TimeAttendance> Attendances { get; set; } = new();
    public List<Shift> PreviousMonthOvernightShifts { get; set; } = new();
    
    public int SelectedYear { get; set; }
    public int SelectedMonth { get; set; }
    
    // Payroll settings (user-configurable)
    public TimeOnly NightStartTime { get; set; } = new TimeOnly(22, 0);
    public TimeOnly NightEndTime { get; set; } = new TimeOnly(6, 0);
    
    /// <summary>
    /// Data source: "shift" = from shifts, "attendance" = from time attendance
    /// </summary>
    public string DataSource { get; set; } = "shift";
    
    /// <summary>
    /// Whether payroll has been calculated
    /// </summary>
    public bool IsCalculated { get; set; } = false;
    
    /// <summary>
    /// ID of the loaded saved payroll (if any)
    /// </summary>
    public int? LoadedPayrollId { get; set; }
    
    /// <summary>
    /// Name of the loaded saved payroll
    /// </summary>
    public string? LoadedPayrollName { get; set; }
    
    // Calculated payroll data per employee
    public List<EmployeePayroll> EmployeePayrolls { get; set; } = new();
    
    // Saved payrolls for this month
    public List<SavedPayroll> SavedPayrolls { get; set; } = new();
    
    // Helper properties
    public int DaysInMonth => DateTime.DaysInMonth(SelectedYear, SelectedMonth);
    public string MonthName => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");
    public DateOnly FirstDayOfMonth => new DateOnly(SelectedYear, SelectedMonth, 1);
    public DateOnly LastDayOfMonth => new DateOnly(SelectedYear, SelectedMonth, DaysInMonth);
}

public class EmployeePayroll
{
    public Employee Employee { get; set; } = null!;
    
    // Work days
    public int WorkedDays { get; set; }
    
    // Hours
    public decimal TotalWorkedHours { get; set; }
    public decimal RequiredHours { get; set; }  // Hedef saat
    
    // Overtime (calculated)
    public decimal OvertimeHours => Math.Max(0, TotalWorkedHours - RequiredHours);
    
    // Special hours
    public decimal WeekendHours { get; set; }
    public decimal HolidayHours { get; set; }
    public decimal NightHours { get; set; }
    
    // Day off count
    public int DayOffCount { get; set; }
    
    // Shift/Attendance details for display (daily breakdown)
    public List<ShiftDetail> ShiftDetails { get; set; } = new();
}

public class ShiftDetail
{
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public decimal TotalHours { get; set; }
    public decimal NightHours { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsHoliday { get; set; }
    public bool IsDayOff { get; set; }
    public bool SpansNextDay { get; set; }
    public string? HolidayName { get; set; }
    public string? Note { get; set; }
}
