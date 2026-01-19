using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Leave type definition (İzin Türü)
/// </summary>
public class LeaveType
{
    public int Id { get; set; }
    
    /// <summary>
    /// Organization ID (null for system-wide defaults)
    /// </summary>
    public int? OrganizationId { get; set; }
    
    /// <summary>
    /// Abbreviation code (e.g., "Yİ", "Hİ", "Eİ")
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Turkish name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string NameTr { get; set; } = string.Empty;
    
    /// <summary>
    /// English name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string NameEn { get; set; } = string.Empty;
    
    /// <summary>
    /// Category for grouping (e.g., "annual", "maternity", "health", "excuse", "military", "other")
    /// </summary>
    [MaxLength(30)]
    public string Category { get; set; } = "other";
    
    /// <summary>
    /// Is this a paid leave?
    /// </summary>
    public bool IsPaid { get; set; } = true;
    
    /// <summary>
    /// Default duration in days (0 = variable)
    /// </summary>
    public int DefaultDays { get; set; } = 0;
    
    /// <summary>
    /// Display color (hex)
    /// </summary>
    [MaxLength(10)]
    public string Color { get; set; } = "#9333ea"; // Purple default
    
    /// <summary>
    /// Sort order for display
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    /// <summary>
    /// Is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Is this a system default (cannot be deleted)
    /// </summary>
    public bool IsSystem { get; set; } = false;
    
    // Navigation
    public virtual Organization? Organization { get; set; }
}

