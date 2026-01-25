using Microsoft.AspNetCore.Identity;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Custom user entity extending IdentityUser
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    
    /// <summary>
    /// User's preferred language (tr or en)
    /// </summary>
    public string Language { get; set; } = "tr";
    
    /// <summary>
    /// User plan type: Free (10 employees), Premium (unlimited)
    /// </summary>
    public UserPlan Plan { get; set; } = UserPlan.Free;
    
    /// <summary>
    /// Custom employee limit (overrides plan default if set)
    /// Null means use the plan's default limit
    /// </summary>
    public int? CustomEmployeeLimit { get; set; }
    
    /// <summary>
    /// Whether user can access Attendance module (default: true for registered)
    /// </summary>
    public bool CanAccessAttendance { get; set; } = true;
    
    /// <summary>
    /// Whether user can access Payroll module (default: true for registered)
    /// </summary>
    public bool CanAccessPayroll { get; set; } = true;
    
    /// <summary>
    /// Whether user can manage Units module (Premium feature or admin-granted)
    /// </summary>
    public bool CanManageUnits { get; set; } = false;
    
    /// <summary>
    /// Maximum number of units user can create (0 = unlimited, default 5 for premium)
    /// </summary>
    public int UnitLimit { get; set; } = 5;
    
    /// <summary>
    /// Maximum number of employees per unit (0 = unlimited)
    /// </summary>
    public int UnitEmployeeLimit { get; set; } = 0;
    
    // ============ Temizlik Çizelgesi Modülü Limitleri ============
    
    /// <summary>
    /// Temizlik modülüne erişim hakkı
    /// </summary>
    public bool CanAccessCleaning { get; set; } = true;
    
    /// <summary>
    /// Maksimum çizelge sayısı (Free: 1, Premium: sınırsız)
    /// </summary>
    public int CleaningScheduleLimit { get; set; } = 1;
    
    /// <summary>
    /// Çizelge başına maksimum madde sayısı (Free: 5, Registered: 10, Premium: 25)
    /// </summary>
    public int CleaningItemLimit { get; set; } = 10;
    
    /// <summary>
    /// Aylık QR erişim limiti (Unregistered: 100, Free: 500, Premium: 3000)
    /// </summary>
    public int CleaningQrAccessLimit { get; set; } = 500;
    
    /// <summary>
    /// Frekans seçimi yapabilme hakkı (Unregistered: false)
    /// </summary>
    public bool CanSelectCleaningFrequency { get; set; } = true;
    
    /// <summary>
    /// Çizelge gruplama hakkı (sadece Premium)
    /// </summary>
    public bool CanGroupCleaningSchedules { get; set; } = false;
    
    /// <summary>
    /// When the premium subscription expires (null for free)
    /// </summary>
    public DateTime? PremiumExpiresAt { get; set; }
    
    /// <summary>
    /// Admin notes about the user
    /// </summary>
    public string? AdminNotes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();
    
    /// <summary>
    /// Get the effective employee limit for this user
    /// </summary>
    public int GetEffectiveEmployeeLimit(int defaultFreeLimit = 10, int premiumLimit = 100)
    {
        // Custom limit overrides everything
        if (CustomEmployeeLimit.HasValue)
            return CustomEmployeeLimit.Value;
        
        // Otherwise use plan default
        return Plan switch
        {
            UserPlan.Premium => premiumLimit,
            _ => defaultFreeLimit
        };
    }
}

public enum UserPlan
{
    Free = 0,      // 10 employees (default), attendance & payroll allowed
    Premium = 1    // Unlimited employees
}


