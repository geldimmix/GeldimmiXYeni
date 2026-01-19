using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// System-wide settings stored in database
/// </summary>
public class SystemSettings
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;
    
    [Required]
    public string Value { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Static keys for easy access
    public static class Keys
    {
        public const string GuestEmployeeLimit = "GuestEmployeeLimit";
        public const string RegisteredEmployeeLimit = "RegisteredEmployeeLimit";
        public const string PremiumEmployeeLimit = "PremiumEmployeeLimit";
        public const string SiteName = "SiteName";
        public const string MaintenanceMode = "MaintenanceMode";
    }
}

