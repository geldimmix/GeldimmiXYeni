using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Employee entity - personnel who will be assigned shifts
/// </summary>
public class Employee
{
    public int Id { get; set; }
    
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// Unit assignment (Premium feature)
    /// </summary>
    public int? UnitId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    
    /// <summary>
    /// Title/Position (e.g., "Hemşire", "Doktor")
    /// </summary>
    [MaxLength(100)]
    public string? Title { get; set; }
    
    /// <summary>
    /// Identity number or registration number (TC Kimlik or Sicil No)
    /// </summary>
    [MaxLength(50)]
    public string? IdentityNo { get; set; }
    
    /// <summary>
    /// Email address
    /// </summary>
    [MaxLength(100)]
    public string? Email { get; set; }
    
    /// <summary>
    /// Phone number
    /// </summary>
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    /// <summary>
    /// Daily work hours requirement
    /// </summary>
    public decimal DailyWorkHours { get; set; } = 8;
    
    /// <summary>
    /// Override weekly work hours (null = use organization default)
    /// </summary>
    public decimal? WeeklyWorkHours { get; set; }
    
    /// <summary>
    /// Weekend work mode: 0=Does not work on weekends, 1=Works both days, 2=Only Saturday, 3=Saturday specific hours
    /// </summary>
    public int WeekendWorkMode { get; set; } = 0;
    
    /// <summary>
    /// Saturday work hours (when WeekendWorkMode is 3)
    /// </summary>
    public decimal? SaturdayWorkHours { get; set; }
    
    /// <summary>
    /// Display color for the employee in calendar (hex color)
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }
    
    /// <summary>
    /// Position type: 4A (Memur), 4B (Sözleşmeli), 4D (696 KHK), Academic
    /// </summary>
    [MaxLength(20)]
    public string? PositionType { get; set; }
    
    /// <summary>
    /// Academic title (only when PositionType is "Academic")
    /// Prof, Doçent, Dr. Öğretim Üyesi, Araştırma Görevlisi, Araştırma Görevlisi (Dr.)
    /// </summary>
    [MaxLength(50)]
    public string? AcademicTitle { get; set; }
    
    /// <summary>
    /// Shift score for fair distribution (default 100)
    /// </summary>
    public int ShiftScore { get; set; } = 100;
    
    /// <summary>
    /// Non-Health Services class (SH Dışı)
    /// </summary>
    public bool IsNonHealthServices { get; set; } = false;
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
    public virtual Unit? Unit { get; set; }
    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public virtual ICollection<Leave> Leaves { get; set; } = new List<Leave>();
    public virtual ICollection<EmployeeAvailability> Availabilities { get; set; } = new List<EmployeeAvailability>();
}

