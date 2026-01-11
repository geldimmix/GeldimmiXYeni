using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Shift/Duty assignment for an employee on a specific date
/// </summary>
public class Shift
{
    public int Id { get; set; }
    
    public int EmployeeId { get; set; }
    public int? ShiftTemplateId { get; set; }
    
    /// <summary>
    /// Date of the shift
    /// </summary>
    public DateOnly Date { get; set; }
    
    /// <summary>
    /// Shift start time
    /// </summary>
    public TimeOnly StartTime { get; set; }
    
    /// <summary>
    /// Shift end time
    /// </summary>
    public TimeOnly EndTime { get; set; }
    
    /// <summary>
    /// Whether the shift spans to the next day
    /// </summary>
    public bool SpansNextDay { get; set; }
    
    /// <summary>
    /// Break duration in minutes
    /// </summary>
    public int BreakMinutes { get; set; }
    
    /// <summary>
    /// Calculated total work hours (excluding break)
    /// </summary>
    public decimal TotalHours { get; set; }
    
    /// <summary>
    /// Calculated night hours
    /// </summary>
    public decimal NightHours { get; set; }
    
    /// <summary>
    /// Whether this is a weekend shift
    /// </summary>
    public bool IsWeekend { get; set; }
    
    /// <summary>
    /// Whether this is a holiday shift
    /// </summary>
    public bool IsHoliday { get; set; }
    
    /// <summary>
    /// Whether this is a day off (employee not working, reduces required hours)
    /// </summary>
    public bool IsDayOff { get; set; }
    
    /// <summary>
    /// Optional notes
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Employee Employee { get; set; } = null!;
    public virtual ShiftTemplate? ShiftTemplate { get; set; }
}

