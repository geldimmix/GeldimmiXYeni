using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Shift template for quick shift assignment
/// </summary>
public class ShiftTemplate
{
    public int Id { get; set; }
    
    /// <summary>
    /// Organization ID (null = global/system template)
    /// </summary>
    public int? OrganizationId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Localized name key for global templates (e.g., "shift.morning")
    /// </summary>
    [MaxLength(100)]
    public string? NameKey { get; set; }
    
    /// <summary>
    /// Shift start time
    /// </summary>
    public TimeOnly StartTime { get; set; }
    
    /// <summary>
    /// Shift end time
    /// </summary>
    public TimeOnly EndTime { get; set; }
    
    /// <summary>
    /// Whether the shift spans to the next day (e.g., 16:00 - 08:00)
    /// </summary>
    public bool SpansNextDay { get; set; }
    
    /// <summary>
    /// Break duration in minutes (overrides organization default)
    /// </summary>
    public int? BreakMinutes { get; set; }
    
    /// <summary>
    /// Display color (hex)
    /// </summary>
    [MaxLength(7)]
    public string Color { get; set; } = "#3B82F6";
    
    /// <summary>
    /// Whether this is a global (system) template
    /// </summary>
    public bool IsGlobal { get; set; }
    
    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Organization? Organization { get; set; }
}


