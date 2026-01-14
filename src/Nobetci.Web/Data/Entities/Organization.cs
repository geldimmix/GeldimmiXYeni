using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Organization/Company entity - each user has at least one organization
/// </summary>
public class Organization
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Owner user ID (nullable for guest sessions)
    /// </summary>
    public string? UserId { get; set; }
    
    /// <summary>
    /// Guest session identifier (for unregistered users)
    /// </summary>
    public string? GuestSessionId { get; set; }
    
    /// <summary>
    /// Daily work hours target (default 8)
    /// </summary>
    public decimal DailyWorkHours { get; set; } = 8;
    
    /// <summary>
    /// Weekly work hours target (default 45)
    /// </summary>
    public decimal WeeklyWorkHours { get; set; } = 45;
    
    /// <summary>
    /// Break duration in minutes (included in work hours)
    /// </summary>
    public int BreakMinutes { get; set; } = 60;
    
    /// <summary>
    /// Night shift start time (default 20:00)
    /// </summary>
    public TimeOnly NightStartTime { get; set; } = new TimeOnly(20, 0);
    
    /// <summary>
    /// Night shift end time (default 06:00)
    /// </summary>
    public TimeOnly NightEndTime { get; set; } = new TimeOnly(6, 0);
    
    /// <summary>
    /// Weekend days (comma separated: 0=Sunday, 6=Saturday, default "0,6")
    /// </summary>
    public string WeekendDays { get; set; } = "0,6";
    
    /// <summary>
    /// Overtime calculation mode: Daily or Monthly
    /// </summary>
    public OvertimeCalcMode OvertimeCalcMode { get; set; } = OvertimeCalcMode.Monthly;
    
    /// <summary>
    /// Whether default templates have been initialized for this organization
    /// </summary>
    public bool DefaultTemplatesInitialized { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ApplicationUser? User { get; set; }
    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public virtual ICollection<ShiftTemplate> ShiftTemplates { get; set; } = new List<ShiftTemplate>();
    public virtual ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();
}

public enum OvertimeCalcMode
{
    Daily = 0,
    Monthly = 1
}


