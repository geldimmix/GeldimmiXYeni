using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

public class AdminStatsViewModel
{
    public int TotalVisits { get; set; }
    public int TodayVisits { get; set; }
    public int WeekVisits { get; set; }
    public int MonthVisits { get; set; }
    public int UniqueIPs { get; set; }
    public List<PageStat> TopPages { get; set; } = new();
    public List<DeviceStat> DeviceStats { get; set; } = new();
    public List<BrowserStat> BrowserStats { get; set; } = new();
    public int TotalPages { get; set; }
    public int PublishedPages { get; set; }
    public int TotalEmployees { get; set; }
    public int TotalOrganizations { get; set; }
}

public class PageStat
{
    public string Path { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DeviceStat
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class BrowserStat
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class QuickUpdateUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public int? IntValue { get; set; }
    public bool? BoolValue { get; set; }
    public string? StringValue { get; set; }
}

public class UserListItem
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserPlan Plan { get; set; }
    public int? CustomEmployeeLimit { get; set; }
    public int UnitLimit { get; set; }
    public int UnitEmployeeLimit { get; set; }
    public bool CanAccessAttendance { get; set; }
    public bool CanAccessPayroll { get; set; }
    public bool CanManageUnits { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? AdminNotes { get; set; }
    public int OrganizationCount { get; set; }
    public int EmployeeCount { get; set; }
    
    public int GetEffectiveLimit(int registeredLimit, int premiumLimit)
    {
        if (CustomEmployeeLimit.HasValue)
            return CustomEmployeeLimit.Value;
        
        return Plan switch
        {
            UserPlan.Premium => premiumLimit,
            _ => registeredLimit
        };
    }
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserPlan Plan { get; set; }
    public int? CustomEmployeeLimit { get; set; }
    public int UnitLimit { get; set; } = 5;
    public int UnitEmployeeLimit { get; set; } = 0;
    public bool CanAccessAttendance { get; set; } = true;
    public bool CanAccessPayroll { get; set; } = true;
    public bool CanManageUnits { get; set; } = false;
    
    // Temizlik Çizelgesi Limitleri
    public bool CanAccessCleaning { get; set; } = true;
    public int CleaningScheduleLimit { get; set; } = 1;
    public int CleaningItemLimit { get; set; } = 10;
    public int CleaningQrAccessLimit { get; set; } = 500;
    public bool CanSelectCleaningFrequency { get; set; } = true;
    public bool CanGroupCleaningSchedules { get; set; } = false;
    
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<OrganizationSummary> Organizations { get; set; } = new();
}

public class OrganizationSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
}

/// <summary>
/// Dinamik sistem ayarları görünümü modeli
/// </summary>
public class SystemSettingsViewModel
{
    public Dictionary<string, List<Nobetci.Web.Data.Entities.SystemSettings>> SettingsByCategory { get; set; } = new();
    public Dictionary<string, string> CategoryNames { get; set; } = new();
}

public class UpdateSettingRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CreateSettingRequest
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? DataType { get; set; }
}

public class AdminUserViewModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = AdminRoles.Admin;
    public bool IsActive { get; set; } = true;
}

public class SelectListItem
{
    public string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class ActivityLogsViewModel
{
    public List<ActivityLogItem> Logs { get; set; } = new();
    public List<SelectListItem> Users { get; set; } = new();
    public List<SelectListItem> ActivityTypes { get; set; } = new();
    public string? SelectedUserId { get; set; }
    public int? SelectedActivityType { get; set; }
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; } = 0;
}

public class ActivityLogItem
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public Nobetci.Web.Data.Entities.ActivityType ActivityType { get; set; }
    public string ActivityTypeName { get; set; } = string.Empty;
    public string ActivityTypeIcon { get; set; } = string.Empty;
    public string ActivityTypeColor { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Blog Management ViewModels
public class BlogPostsViewModel
{
    public List<Nobetci.Web.Data.Entities.BlogPost> Posts { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalCount { get; set; } = 0;
}

public class BlogPostEditViewModel
{
    public int Id { get; set; }
    
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "URL Slug zorunludur")]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug sadece küçük harf, rakam ve tire içerebilir")]
    public string Slug { get; set; } = string.Empty;
    
    // Turkish content
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Türkçe başlık zorunludur")]
    [System.ComponentModel.DataAnnotations.MaxLength(300)]
    public string TitleTr { get; set; } = string.Empty;
    
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? ExcerptTr { get; set; }
    
    public string? ContentTr { get; set; }
    
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? KeywordsTr { get; set; }
    
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? MetaDescriptionTr { get; set; }
    
    // English content
    [System.ComponentModel.DataAnnotations.MaxLength(300)]
    public string? TitleEn { get; set; }
    
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? ExcerptEn { get; set; }
    
    public string? ContentEn { get; set; }
    
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? KeywordsEn { get; set; }
    
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? MetaDescriptionEn { get; set; }
    
    // SEO & Meta
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? OgImageUrl { get; set; }
    
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? CanonicalUrl { get; set; }
    
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? RobotsMeta { get; set; }
    
    // Publishing
    public bool IsPublished { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
    
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? AuthorName { get; set; }
    
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; } = 0;
}
