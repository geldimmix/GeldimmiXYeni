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
    public List<Leave> Leaves { get; set; } = new();
    
    // Unit filtering (Premium feature)
    public List<Unit> Units { get; set; } = new();
    public int? SelectedUnitId { get; set; }
    public bool IsPremium { get; set; }
    public bool IsRegistered { get; set; }
    public bool CanAccessAttendance { get; set; }
    public bool CanAccessPayroll { get; set; }
    public bool CanManageUnits { get; set; }
    public bool CanAccessCleaning { get; set; } = true;
    public int EmployeeLimit { get; set; }
    public int UnitLimit { get; set; }
    public int TotalEmployeeCount { get; set; }
    
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

    // Calculated bordro summaries
    public List<BordroSummary> BordroSummaries { get; set; } = new();

    public PayrollOptions PayrollOptions { get; set; } = new();

    public BordroOptions BordroOptions { get; set; } = new();
    public List<Bordro4AResult> Bordro4AResults { get; set; } = new();
    public List<Bordro4BResult> Bordro4BResults { get; set; } = new();
    
    // Saved payrolls for this month
    public List<SavedPayroll> SavedPayrolls { get; set; } = new();
    
    // Helper properties
    public int EmployeeCount => Employees.Count;
    public int DaysInMonth => DateTime.DaysInMonth(SelectedYear, SelectedMonth);
    public string MonthName => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");
    public DateOnly FirstDayOfMonth => new DateOnly(SelectedYear, SelectedMonth, 1);
    public DateOnly LastDayOfMonth => new DateOnly(SelectedYear, SelectedMonth, DaysInMonth);
    
    // Helper methods
    public Leave? GetLeaveForEmployeeOnDate(int employeeId, DateOnly date)
    {
        return Leaves.FirstOrDefault(l => l.EmployeeId == employeeId && l.Date == date);
    }
    
    public bool IsWeekend(DateOnly date)
    {
        var weekendDays = Organization.WeekendDays.Split(',').Select(int.Parse).ToList();
        return weekendDays.Contains((int)date.DayOfWeek);
    }
    
    public bool IsHoliday(DateOnly date)
    {
        return Holidays.Any(h => h.Date == date);
    }
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
    public decimal OvertimeHours { get; set; }
    public decimal RawOvertimeHours { get; set; }
    public decimal HolidayOvertimeHours { get; set; }
    public decimal HolidayDifferenceHours { get; set; }
    public decimal NormalOvertimeHours { get; set; }
    public decimal IntensiveOvertimeHours { get; set; }
    public decimal NormalHolidayHours { get; set; }
    public decimal IntensiveHolidayHours { get; set; }
    public bool IsIntensiveCare { get; set; }
    
    // Special hours
    public decimal WeekendHours { get; set; }
    public decimal HolidayHours { get; set; }
    public decimal NightHours { get; set; }

    public int TicketCount { get; set; }
    public int TransportationDays { get; set; }
    
    // Day off count
    public int DayOffCount { get; set; }
    public int LeaveDays { get; set; }
    public int AnnualLeaveDays { get; set; }
    public int SickLeaveDays { get; set; }
    
    // Shift/Attendance details for display (daily breakdown)
    public List<ShiftDetail> ShiftDetails { get; set; } = new();

    // Internal calculation segments for daily overtime
    public List<WorkSegment> CalculationSegments { get; set; } = new();
}

public class ShiftDetail
{
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public decimal TotalHours { get; set; }
    public decimal NormalHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal NightHours { get; set; }
    public decimal WeekendHours { get; set; }
    public decimal HolidayHours { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsHoliday { get; set; }
    public bool IsIntensiveCare { get; set; }
    public bool IsDayOff { get; set; }
    public bool IsLeave { get; set; }
    public string? LeaveCode { get; set; }
    public string? LeaveColor { get; set; }
    public bool SpansNextDay { get; set; }
    public string? HolidayName { get; set; }
    public string? Note { get; set; }
}

public class BordroSummary
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeTitle { get; set; }
    public string? PositionType { get; set; }
    public string? UnitName { get; set; }
    public decimal UnitCoefficient { get; set; } = 1.0m;
    public decimal BaseHourlyRate { get; set; }
    public decimal TotalHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal HolidayHours { get; set; }
    public decimal WeekendHours { get; set; }
    public decimal NightHours { get; set; }
    public decimal BasePay { get; set; }
    public decimal OvertimePremium { get; set; }
    public decimal HolidayPremium { get; set; }
    public decimal WeekendPremium { get; set; }
    public decimal NightPremium { get; set; }
    public decimal GrossPay { get; set; }
}

public class WorkSegment
{
    public DateOnly Date { get; set; }
    public decimal Hours { get; set; }
    public bool IsIntensiveCare { get; set; }
}
