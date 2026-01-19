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
    Task<int> GetGuestEmployeeLimitAsync();
    Task<int> GetRegisteredEmployeeLimitAsync();
    Task<int> GetPremiumEmployeeLimitAsync();
    Task<string?> GetSettingAsync(string key);
    Task SetSettingAsync(string key, string value);
    void ClearCache();
}

public class SystemSettingsService : ISystemSettingsService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private const string CachePrefix = "SystemSettings_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SystemSettingsService(ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration)
    {
        _context = context;
        _cache = cache;
        _configuration = configuration;
    }

    public async Task<int> GetGuestEmployeeLimitAsync()
    {
        var value = await GetSettingAsync(SystemSettings.Keys.GuestEmployeeLimit);
        if (int.TryParse(value, out var limit))
            return limit;
        
        // Fallback to config
        return _configuration.GetValue<int>("AppSettings:GuestEmployeeLimit", 5);
    }

    public async Task<int> GetRegisteredEmployeeLimitAsync()
    {
        var value = await GetSettingAsync(SystemSettings.Keys.RegisteredEmployeeLimit);
        if (int.TryParse(value, out var limit))
            return limit;
        
        // Fallback to config
        return _configuration.GetValue<int>("AppSettings:RegisteredEmployeeLimit", 10);
    }

    public async Task<int> GetPremiumEmployeeLimitAsync()
    {
        var value = await GetSettingAsync(SystemSettings.Keys.PremiumEmployeeLimit);
        if (int.TryParse(value, out var limit))
            return limit;
        
        // Fallback to config
        return _configuration.GetValue<int>("AppSettings:PremiumEmployeeLimit", 100);
    }

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

    public async Task SetSettingAsync(string key, string value)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting == null)
        {
            setting = new SystemSettings
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            };
            _context.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync();
        
        // Clear cache
        _cache.Remove(CachePrefix + key);
    }

    public void ClearCache()
    {
        // Clear all known settings from cache
        var keys = new[]
        {
            SystemSettings.Keys.GuestEmployeeLimit,
            SystemSettings.Keys.RegisteredEmployeeLimit,
            SystemSettings.Keys.PremiumEmployeeLimit,
            SystemSettings.Keys.SiteName,
            SystemSettings.Keys.MaintenanceMode
        };
        
        foreach (var key in keys)
        {
            _cache.Remove(CachePrefix + key);
        }
    }
}

