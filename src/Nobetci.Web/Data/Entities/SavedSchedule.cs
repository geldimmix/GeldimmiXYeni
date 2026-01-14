using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Saved schedule for a specific month/year that can be named and retrieved later
/// Only available for registered users
/// </summary>
public class SavedSchedule
{
    public int Id { get; set; }
    
    /// <summary>
    /// Organization this schedule belongs to
    /// </summary>
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// User-provided name for this schedule
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Year of the schedule
    /// </summary>
    public int Year { get; set; }
    
    /// <summary>
    /// Month of the schedule (1-12)
    /// </summary>
    public int Month { get; set; }
    
    /// <summary>
    /// JSON serialized shift data for quick loading
    /// Contains employee shifts for this month
    /// </summary>
    public string ShiftDataJson { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional description/notes for this schedule
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
}

