using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

/// <summary>
/// Ana temizlik yönetim sayfası ViewModel
/// </summary>
public class CleaningViewModel
{
    public List<CleaningSchedule> Schedules { get; set; } = new();
    public List<CleaningScheduleGroup> Groups { get; set; } = new();
    public CleaningLimits Limits { get; set; } = new();
    public int PendingRecordsCount { get; set; }
    public int MonthlyQrAccessCount { get; set; }
    public bool IsRegistered { get; set; }
    public bool IsPremium { get; set; }
    public bool IsTurkish { get; set; } = true;
    
    public int ScheduleCount => Schedules.Count;
    public bool CanAddSchedule => ScheduleCount < Limits.MaxSchedules;
}

/// <summary>
/// Temizlik modülü limitleri
/// </summary>
public class CleaningLimits
{
    public int MaxSchedules { get; set; } = 1;
    public int MaxItemsPerSchedule { get; set; } = 5;
    public int MaxQrAccessPerMonth { get; set; } = 100;
    public bool CanSelectFrequency { get; set; } = false;
    public bool CanGroupSchedules { get; set; } = false;
}

/// <summary>
/// QR erişim sayfası ViewModel
/// </summary>
public class QrAccessViewModel
{
    public string QrCode { get; set; } = string.Empty;
    public string ScheduleName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool HasAccessCode { get; set; }
    public bool IsTurkish { get; set; } = true;
}

/// <summary>
/// QR limit aşıldı sayfası ViewModel
/// </summary>
public class QrLimitViewModel
{
    public string ScheduleName { get; set; } = string.Empty;
    public int Limit { get; set; }
}

