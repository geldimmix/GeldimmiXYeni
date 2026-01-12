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
    /// User plan type: Free (10 employees), Freemium (25 employees), Premium (unlimited)
    /// </summary>
    public UserPlan Plan { get; set; } = UserPlan.Free;
    
    /// <summary>
    /// When the premium subscription expires (null for free/freemium)
    /// </summary>
    public DateTime? PremiumExpiresAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();
}

public enum UserPlan
{
    Free = 0,      // 10 employees, no timesheet
    Freemium = 1,  // 25 employees, with timesheet
    Premium = 2    // Unlimited employees
}


