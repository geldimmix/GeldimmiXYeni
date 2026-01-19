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
    public bool CanAccessAttendance { get; set; }
    public bool CanAccessPayroll { get; set; }
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
    public bool CanAccessAttendance { get; set; } = true;
    public bool CanAccessPayroll { get; set; } = true;
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

public class SystemSettingsViewModel
{
    public int GuestEmployeeLimit { get; set; } = 5;
    public int RegisteredEmployeeLimit { get; set; } = 10;
    public int PremiumEmployeeLimit { get; set; } = 100;
    public string? SiteName { get; set; } = "Geldimmi";
    public bool MaintenanceMode { get; set; } = false;
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

