using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Unit/Department entity - for premium feature (multiple departments)
/// Allows organizing employees, shifts, and payroll by department/unit
/// </summary>
public class Unit
{
    public int Id { get; set; }
    
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// Optional Unit Type (e.g., Clinic, Polyclinic, ICU)
    /// </summary>
    public int? UnitTypeId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of this unit
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Coefficient multiplier for this unit (e.g., 1.0, 1.5, 1.75)
    /// Used for payroll calculations - units like ICU or Radiation may have higher coefficients
    /// </summary>
    public decimal Coefficient { get; set; } = 1.0m;
    
    /// <summary>
    /// Color for UI display (hex color)
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }
    
    /// <summary>
    /// Whether this is the default unit for the organization
    /// </summary>
    public bool IsDefault { get; set; }
    
    /// <summary>
    /// Whether this unit is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
    public virtual UnitType? UnitType { get; set; }
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
