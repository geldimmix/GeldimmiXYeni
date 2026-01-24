using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// User API credentials for Basic Authentication access to work hours API
/// </summary>
public class UserApiCredential
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// API username chosen by user
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ApiUsername { get; set; } = string.Empty;
    
    /// <summary>
    /// API password hash (BCrypt)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ApiPasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Monthly request limit (calculated: EmployeeLimit x DaysInMonth x 2)
    /// </summary>
    public int MonthlyRequestLimit { get; set; } = 0;
    
    /// <summary>
    /// Current month's request count
    /// </summary>
    public int CurrentMonthRequests { get; set; } = 0;
    
    /// <summary>
    /// When the monthly counter resets (first day of next month)
    /// </summary>
    public DateTime MonthlyResetDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether API access is enabled
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Last time API was used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
    
    /// <summary>
    /// Total requests made (lifetime)
    /// </summary>
    public int TotalRequests { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Organization Organization { get; set; } = null!;
}

