using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Saved payroll calculation result
/// </summary>
public class SavedPayroll
{
    public int Id { get; set; }
    
    public int OrganizationId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public int Year { get; set; }
    public int Month { get; set; }
    
    /// <summary>
    /// Data source: "shift" or "attendance"
    /// </summary>
    [MaxLength(20)]
    public string DataSource { get; set; } = "shift";
    
    /// <summary>
    /// Night shift start hour (e.g., 22)
    /// </summary>
    public int NightStartHour { get; set; } = 22;
    
    /// <summary>
    /// Night shift end hour (e.g., 6)
    /// </summary>
    public int NightEndHour { get; set; } = 6;
    
    /// <summary>
    /// JSON serialized payroll data
    /// </summary>
    public string PayrollDataJson { get; set; } = "[]";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public virtual Organization Organization { get; set; } = null!;
}

/// <summary>
/// Individual employee payroll entry (stored in JSON)
/// </summary>
public class SavedPayrollEntry
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeTitle { get; set; }
    
    public int WorkedDays { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public decimal NightHours { get; set; }
    public decimal WeekendHours { get; set; }
    public decimal HolidayHours { get; set; }
    public int DayOffCount { get; set; }
    
    /// <summary>
    /// Daily breakdown
    /// </summary>
    public List<DailyEntry> DailyEntries { get; set; } = new();
}

public class DailyEntry
{
    public string Date { get; set; } = string.Empty;
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public decimal Hours { get; set; }
    public decimal NightHours { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsHoliday { get; set; }
    public bool IsDayOff { get; set; }
    public string? Note { get; set; }
}

