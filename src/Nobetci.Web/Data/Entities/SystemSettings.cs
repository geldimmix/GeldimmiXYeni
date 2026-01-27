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
    
    /// <summary>
    /// Category for grouping settings in admin panel
    /// </summary>
    [MaxLength(50)]
    public string Category { get; set; } = "general";
    
    /// <summary>
    /// Data type hint for UI (int, bool, string, decimal)
    /// </summary>
    [MaxLength(20)]
    public string DataType { get; set; } = "string";
    
    /// <summary>
    /// Display order in admin panel
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Static keys for easy access
    public static class Keys
    {
        // ========== Personel Limitleri ==========
        public const string GuestEmployeeLimit = "GuestEmployeeLimit";
        public const string RegisteredEmployeeLimit = "RegisteredEmployeeLimit";
        public const string PremiumEmployeeLimit = "PremiumEmployeeLimit";
        
        // ========== Genel Ayarlar ==========
        public const string SiteName = "SiteName";
        public const string MaintenanceMode = "MaintenanceMode";
        
        // ========== Varsayılan Çalışma Ayarları ==========
        public const string DefaultDailyWorkHours = "DefaultDailyWorkHours";
        public const string DefaultWeeklyWorkHours = "DefaultWeeklyWorkHours";
        public const string DefaultBreakMinutes = "DefaultBreakMinutes";
        public const string DefaultNightStartHour = "DefaultNightStartHour";
        public const string DefaultNightEndHour = "DefaultNightEndHour";
        public const string DefaultWeekendDays = "DefaultWeekendDays";
        
        // ========== Temizlik Modülü - Kayıtsız Kullanıcı Limitleri ==========
        public const string UnregisteredMaxSchedules = "UnregisteredMaxSchedules";
        public const string UnregisteredMaxItemsPerSchedule = "UnregisteredMaxItemsPerSchedule";
        public const string UnregisteredMaxQrAccessPerMonth = "UnregisteredMaxQrAccessPerMonth";
        
        // ========== Temizlik Modülü - Kayıtlı (Free) Kullanıcı Varsayılanları ==========
        public const string RegisteredDefaultScheduleLimit = "RegisteredDefaultScheduleLimit";
        public const string RegisteredDefaultItemLimit = "RegisteredDefaultItemLimit";
        public const string RegisteredDefaultQrAccessLimit = "RegisteredDefaultQrAccessLimit";
        
        // ========== Temizlik Modülü - Premium Kullanıcı Varsayılanları ==========
        public const string PremiumDefaultScheduleLimit = "PremiumDefaultScheduleLimit";
        public const string PremiumDefaultItemLimit = "PremiumDefaultItemLimit";
        public const string PremiumDefaultQrAccessLimit = "PremiumDefaultQrAccessLimit";
        
        // ========== Birim Limitleri ==========
        public const string DefaultUnitLimit = "DefaultUnitLimit";
        public const string DefaultUnitEmployeeLimit = "DefaultUnitEmployeeLimit";
        
        // ========== Güvenlik ==========
        public const string PasswordMinLength = "PasswordMinLength";
        public const string MaxLoginAttempts = "MaxLoginAttempts";
        public const string LockoutMinutes = "LockoutMinutes";
    }
    
    public static class Categories
    {
        public const string General = "general";
        public const string EmployeeLimits = "employee_limits";
        public const string WorkSettings = "work_settings";
        public const string CleaningLimits = "cleaning_limits";
        public const string UnitLimits = "unit_limits";
        public const string Security = "security";
    }
}

