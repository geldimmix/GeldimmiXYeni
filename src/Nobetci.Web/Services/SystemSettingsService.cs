using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Services;

/// <summary>
/// Service for accessing system settings from database with caching
/// </summary>
public interface ISystemSettingsService
{
    // Employee Limits
    Task<int> GetGuestEmployeeLimitAsync();
    Task<int> GetRegisteredEmployeeLimitAsync();
    Task<int> GetPremiumEmployeeLimitAsync();
    
    // Work Settings
    Task<decimal> GetDefaultDailyWorkHoursAsync();
    Task<decimal> GetDefaultWeeklyWorkHoursAsync();
    Task<int> GetDefaultBreakMinutesAsync();
    Task<int> GetDefaultNightStartHourAsync();
    Task<int> GetDefaultNightEndHourAsync();
    Task<string> GetDefaultWeekendDaysAsync();
    
    // Cleaning Module - Unregistered limits
    Task<int> GetUnregisteredMaxSchedulesAsync();
    Task<int> GetUnregisteredMaxItemsPerScheduleAsync();
    Task<int> GetUnregisteredMaxQrAccessPerMonthAsync();
    
    // Cleaning Module - Registered defaults
    Task<int> GetRegisteredDefaultScheduleLimitAsync();
    Task<int> GetRegisteredDefaultItemLimitAsync();
    Task<int> GetRegisteredDefaultQrAccessLimitAsync();
    
    // Cleaning Module - Premium defaults
    Task<int> GetPremiumDefaultScheduleLimitAsync();
    Task<int> GetPremiumDefaultItemLimitAsync();
    Task<int> GetPremiumDefaultQrAccessLimitAsync();
    
    // Unit Limits
    Task<int> GetDefaultUnitLimitAsync();
    Task<int> GetDefaultUnitEmployeeLimitAsync();
    
    // Security
    Task<int> GetPasswordMinLengthAsync();
    Task<int> GetMaxLoginAttemptsAsync();
    Task<int> GetLockoutMinutesAsync();
    
    // Generic methods
    Task<string?> GetSettingAsync(string key);
    Task<int> GetIntSettingAsync(string key, int defaultValue);
    Task<decimal> GetDecimalSettingAsync(string key, decimal defaultValue);
    Task<bool> GetBoolSettingAsync(string key, bool defaultValue);
    Task SetSettingAsync(string key, string value, string? description = null, string? category = null, string? dataType = null);
    Task<List<SystemSettings>> GetAllSettingsAsync();
    Task<List<SystemSettings>> GetSettingsByCategoryAsync(string category);
    void ClearCache();
}

public class SystemSettingsService : ISystemSettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private const string CachePrefix = "SystemSettings_";
    private const string AllSettingsCacheKey = "SystemSettings_All";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SystemSettingsService(ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration)
    {
        _context = context;
        _cache = cache;
        _configuration = configuration;
    }

    #region Employee Limits

    public async Task<int> GetGuestEmployeeLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.GuestEmployeeLimit, 5);

    public async Task<int> GetRegisteredEmployeeLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.RegisteredEmployeeLimit, 10);

    public async Task<int> GetPremiumEmployeeLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.PremiumEmployeeLimit, 100);

    #endregion

    #region Work Settings

    public async Task<decimal> GetDefaultDailyWorkHoursAsync()
        => await GetDecimalSettingAsync(SystemSettings.Keys.DefaultDailyWorkHours, 8m);

    public async Task<decimal> GetDefaultWeeklyWorkHoursAsync()
        => await GetDecimalSettingAsync(SystemSettings.Keys.DefaultWeeklyWorkHours, 45m);

    public async Task<int> GetDefaultBreakMinutesAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.DefaultBreakMinutes, 60);

    public async Task<int> GetDefaultNightStartHourAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.DefaultNightStartHour, 20);

    public async Task<int> GetDefaultNightEndHourAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.DefaultNightEndHour, 6);

    public async Task<string> GetDefaultWeekendDaysAsync()
        => await GetSettingAsync(SystemSettings.Keys.DefaultWeekendDays) ?? "0,6";

    #endregion

    #region Cleaning Module - Unregistered

    public async Task<int> GetUnregisteredMaxSchedulesAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.UnregisteredMaxSchedules, 1);

    public async Task<int> GetUnregisteredMaxItemsPerScheduleAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.UnregisteredMaxItemsPerSchedule, 5);

    public async Task<int> GetUnregisteredMaxQrAccessPerMonthAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.UnregisteredMaxQrAccessPerMonth, 100);

    #endregion

    #region Cleaning Module - Registered

    public async Task<int> GetRegisteredDefaultScheduleLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.RegisteredDefaultScheduleLimit, 1);

    public async Task<int> GetRegisteredDefaultItemLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.RegisteredDefaultItemLimit, 10);

    public async Task<int> GetRegisteredDefaultQrAccessLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.RegisteredDefaultQrAccessLimit, 500);

    #endregion

    #region Cleaning Module - Premium

    public async Task<int> GetPremiumDefaultScheduleLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.PremiumDefaultScheduleLimit, 100);

    public async Task<int> GetPremiumDefaultItemLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.PremiumDefaultItemLimit, 25);

    public async Task<int> GetPremiumDefaultQrAccessLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.PremiumDefaultQrAccessLimit, 3000);

    #endregion

    #region Unit Limits

    public async Task<int> GetDefaultUnitLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.DefaultUnitLimit, 5);

    public async Task<int> GetDefaultUnitEmployeeLimitAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.DefaultUnitEmployeeLimit, 0);

    #endregion

    #region Security

    public async Task<int> GetPasswordMinLengthAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.PasswordMinLength, 6);

    public async Task<int> GetMaxLoginAttemptsAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.MaxLoginAttempts, 5);

    public async Task<int> GetLockoutMinutesAsync()
        => await GetIntSettingAsync(SystemSettings.Keys.LockoutMinutes, 5);

    #endregion

    #region Generic Methods

    public async Task<string?> GetSettingAsync(string key)
    {
        var cacheKey = CachePrefix + key;
        
        if (_cache.TryGetValue(cacheKey, out string? cachedValue))
            return cachedValue;
        
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        var value = setting?.Value;
        
        if (value != null)
        {
            _cache.Set(cacheKey, value, CacheDuration);
        }
        
        return value;
    }

    public async Task<int> GetIntSettingAsync(string key, int defaultValue)
    {
        var value = await GetSettingAsync(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task<decimal> GetDecimalSettingAsync(string key, decimal defaultValue)
    {
        var value = await GetSettingAsync(key);
        return decimal.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task<bool> GetBoolSettingAsync(string key, bool defaultValue)
    {
        var value = await GetSettingAsync(key);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task SetSettingAsync(string key, string value, string? description = null, string? category = null, string? dataType = null)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting == null)
        {
            setting = new SystemSettings
            {
                Key = key,
                Value = value,
                Description = description,
                Category = category ?? SystemSettings.Categories.General,
                DataType = dataType ?? "string",
                UpdatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            if (description != null) setting.Description = description;
            if (category != null) setting.Category = category;
            if (dataType != null) setting.DataType = dataType;
        }
        
        await _context.SaveChangesAsync();
        
        // Clear cache
        _cache.Remove(CachePrefix + key);
        _cache.Remove(AllSettingsCacheKey);
    }

    public async Task<List<SystemSettings>> GetAllSettingsAsync()
    {
        if (_cache.TryGetValue(AllSettingsCacheKey, out List<SystemSettings>? cached))
            return cached ?? new List<SystemSettings>();
        
        var settings = await _context.SystemSettings
            .OrderBy(s => s.Category)
            .ThenBy(s => s.SortOrder)
            .ToListAsync();
        
        _cache.Set(AllSettingsCacheKey, settings, CacheDuration);
        return settings;
    }

    public async Task<List<SystemSettings>> GetSettingsByCategoryAsync(string category)
    {
        return await _context.SystemSettings
            .Where(s => s.Category == category)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
    }

    public void ClearCache()
    {
        // Clear all known settings from cache
        var keys = typeof(SystemSettings.Keys).GetFields()
            .Select(f => f.GetValue(null)?.ToString())
            .Where(k => k != null);
        
        foreach (var key in keys)
        {
            _cache.Remove(CachePrefix + key);
        }
        
        _cache.Remove(AllSettingsCacheKey);
    }

    #endregion
}

