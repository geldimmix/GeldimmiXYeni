using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
    [JsonPropertyName("employeeId")]
    public int EmployeeId { get; set; }
    
    [JsonPropertyName("employeeName")]
    public string EmployeeName { get; set; } = string.Empty;
    
    [JsonPropertyName("employeeTitle")]
    public string? EmployeeTitle { get; set; }
    
    [JsonPropertyName("workedDays")]
    public int WorkedDays { get; set; }
    
    [JsonPropertyName("totalWorkedHours")]
    public decimal TotalWorkedHours { get; set; }
    
    [JsonPropertyName("requiredHours")]
    public decimal RequiredHours { get; set; }
    
    [JsonPropertyName("overtimeHours")]
    public decimal OvertimeHours { get; set; }
    
    [JsonPropertyName("nightHours")]
    public decimal NightHours { get; set; }
    
    [JsonPropertyName("weekendHours")]
    public decimal WeekendHours { get; set; }
    
    [JsonPropertyName("holidayHours")]
    public decimal HolidayHours { get; set; }
    
    [JsonPropertyName("dayOffCount")]
    public int DayOffCount { get; set; }
    
    /// <summary>
    /// Daily breakdown
    /// </summary>
    [JsonPropertyName("dailyEntries")]
    public List<DailyEntry> DailyEntries { get; set; } = new();
}

public class DailyEntry
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
    
    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }
    
    [JsonPropertyName("endTime")]
    public string? EndTime { get; set; }
    
    [JsonPropertyName("hours")]
    public decimal Hours { get; set; }
    
    [JsonPropertyName("nightHours")]
    public decimal NightHours { get; set; }
    
    [JsonPropertyName("isWeekend")]
    public bool IsWeekend { get; set; }
    
    [JsonPropertyName("isHoliday")]
    public bool IsHoliday { get; set; }
    
    [JsonPropertyName("isDayOff")]
    public bool IsDayOff { get; set; }
    
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

