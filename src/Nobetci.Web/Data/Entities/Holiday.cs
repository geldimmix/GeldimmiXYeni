using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Holiday definition for an organization
/// </summary>
public class Holiday
{
    public int Id { get; set; }
    
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// Holiday date
    /// </summary>
    public DateOnly Date { get; set; }
    
    /// <summary>
    /// Holiday name (e.g., "Yılbaşı", "23 Nisan")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of holiday
    /// </summary>
    public HolidayType Type { get; set; } = HolidayType.OfficialHoliday;
    
    /// <summary>
    /// Whether this is a half-day holiday
    /// </summary>
    public bool IsHalfDay { get; set; }
    
    /// <summary>
    /// If half-day, how many hours should employees work (e.g., 4 hours)
    /// </summary>
    public decimal? HalfDayWorkHours { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
}

public enum HolidayType
{
    OfficialHoliday = 0,  // Resmi Tatil
    AdminHoliday = 1      // İdari Tatil
}


