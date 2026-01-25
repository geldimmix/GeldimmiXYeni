using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Unit Type entity - defines the type/category of a unit
/// Examples: Normal, Clinic, Polyclinic, Radiation Unit, ICU, Operating Room
/// </summary>
public class UnitType
{
    public int Id { get; set; }
    
    public int OrganizationId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// English name for the unit type (for localization)
    /// </summary>
    [MaxLength(100)]
    public string? NameEn { get; set; }
    
    /// <summary>
    /// Description of this unit type
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Default coefficient for this unit type (e.g., 1.0, 1.5, 1.75)
    /// This can be overridden at the unit level
    /// </summary>
    public decimal DefaultCoefficient { get; set; } = 1.0m;
    
    /// <summary>
    /// Color for UI display (hex color)
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }
    
    /// <summary>
    /// Icon name for UI display (e.g., "hospital", "radiation", "heart-pulse")
    /// </summary>
    [MaxLength(50)]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    /// <summary>
    /// Whether this unit type is active and can be assigned to units
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether this is a system-defined type (cannot be deleted)
    /// </summary>
    public bool IsSystem { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}

