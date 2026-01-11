using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Employee availability constraints for smart scheduling
/// </summary>
public class EmployeeAvailability
{
    public int Id { get; set; }
    
    public int EmployeeId { get; set; }
    
    /// <summary>
    /// Constraint type: Weekly (recurring) or Specific (one-time)
    /// </summary>
    public AvailabilityType Type { get; set; }
    
    /// <summary>
    /// For weekly constraints: Day of week (0=Sunday, 6=Saturday)
    /// </summary>
    public DayOfWeek? DayOfWeek { get; set; }
    
    /// <summary>
    /// For specific date constraints
    /// </summary>
    public DateOnly? SpecificDate { get; set; }
    
    /// <summary>
    /// Whether the employee is available (false = unavailable)
    /// </summary>
    public bool IsAvailable { get; set; }
    
    /// <summary>
    /// Time period: All day, Morning, Afternoon, Evening, Night
    /// </summary>
    public TimePeriod TimePeriod { get; set; } = TimePeriod.AllDay;
    
    /// <summary>
    /// Optional reason for unavailability
    /// </summary>
    [MaxLength(200)]
    public string? Reason { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Employee Employee { get; set; } = null!;
}

public enum AvailabilityType
{
    Weekly = 0,    // Recurring weekly constraint (e.g., "Fridays unavailable")
    Specific = 1   // One-time constraint (e.g., "Jan 15 unavailable")
}

public enum TimePeriod
{
    AllDay = 0,
    Morning = 1,    // 06:00 - 12:00
    Afternoon = 2,  // 12:00 - 18:00
    Evening = 3,    // 18:00 - 24:00
    Night = 4       // 00:00 - 06:00
}

