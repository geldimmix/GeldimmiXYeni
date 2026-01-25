using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Services;

/// <summary>
/// Aktivite loglama servisi
/// </summary>
public interface IActivityLogService
{
    Task LogAsync(ActivityType type, string description, string? entityType = null, int? entityId = null, object? details = null);
    Task LogAsync(string userId, int? organizationId, ActivityType type, string description, string? entityType = null, int? entityId = null, object? details = null);
    Task<List<ActivityLog>> GetLogsAsync(int? organizationId = null, string? userId = null, ActivityType? type = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 50);
    Task<int> GetLogCountAsync(int? organizationId = null, string? userId = null, ActivityType? type = null, DateTime? from = null, DateTime? to = null);
}

public class ActivityLogService : IActivityLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActivityLogService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Mevcut kullanıcı bilgilerini kullanarak log kaydı oluştur
    /// </summary>
    public async Task LogAsync(ActivityType type, string description, string? entityType = null, int? entityId = null, object? details = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        string? userId = null;
        int? organizationId = null;

        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            var user = await _context.Users
                .Include(u => u.Organizations)
                .FirstOrDefaultAsync(u => u.UserName == httpContext.User.Identity.Name);

            if (user != null)
            {
                userId = user.Id;
                organizationId = user.Organizations.FirstOrDefault()?.Id;
            }
        }

        await LogInternalAsync(userId, organizationId, type, description, entityType, entityId, details, httpContext);
    }

    /// <summary>
    /// Belirli kullanıcı ve organizasyon için log kaydı oluştur
    /// </summary>
    public async Task LogAsync(string userId, int? organizationId, ActivityType type, string description, string? entityType = null, int? entityId = null, object? details = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        await LogInternalAsync(userId, organizationId, type, description, entityType, entityId, details, httpContext);
    }

    private async Task LogInternalAsync(string? userId, int? organizationId, ActivityType type, string description, string? entityType, int? entityId, object? details, HttpContext? httpContext)
    {
        var log = new ActivityLog
        {
            UserId = userId,
            OrganizationId = organizationId,
            ActivityType = type,
            Description = description,
            EntityType = entityType,
            EntityId = entityId,
            Details = details != null ? JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = false }) : null,
            IpAddress = GetIpAddress(httpContext),
            UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
            CreatedAt = DateTime.UtcNow
        };

        _context.ActivityLogs.Add(log);
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log hatası uygulamayı durdurmamalı
            Console.WriteLine($"Activity log error: {ex.Message}");
        }
    }

    private string? GetIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null) return null;

        // X-Forwarded-For header'ı kontrol et (proxy/load balancer arkasında)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        // X-Real-IP header'ı kontrol et
        var realIp = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Doğrudan bağlantı IP'si
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Logları getir
    /// </summary>
    public async Task<List<ActivityLog>> GetLogsAsync(int? organizationId = null, string? userId = null, ActivityType? type = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 50)
    {
        var query = BuildQuery(organizationId, userId, type, from, to);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(l => l.User)
            .ToListAsync();
    }

    /// <summary>
    /// Log sayısını getir
    /// </summary>
    public async Task<int> GetLogCountAsync(int? organizationId = null, string? userId = null, ActivityType? type = null, DateTime? from = null, DateTime? to = null)
    {
        var query = BuildQuery(organizationId, userId, type, from, to);
        return await query.CountAsync();
    }

    private IQueryable<ActivityLog> BuildQuery(int? organizationId, string? userId, ActivityType? type, DateTime? from, DateTime? to)
    {
        var query = _context.ActivityLogs.AsQueryable();

        if (organizationId.HasValue)
            query = query.Where(l => l.OrganizationId == organizationId);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(l => l.UserId == userId);

        if (type.HasValue)
            query = query.Where(l => l.ActivityType == type);

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        return query;
    }
}

/// <summary>
/// Aktivite türü için Türkçe ve İngilizce açıklamalar
/// </summary>
public static class ActivityTypeExtensions
{
    public static string GetDisplayName(this ActivityType type, bool turkish = true)
    {
        return type switch
        {
            // Personel
            ActivityType.EmployeeCreated => turkish ? "Personel Eklendi" : "Employee Created",
            ActivityType.EmployeeUpdated => turkish ? "Personel Güncellendi" : "Employee Updated",
            ActivityType.EmployeeDeleted => turkish ? "Personel Silindi" : "Employee Deleted",
            
            // Nöbet
            ActivityType.ShiftCreated => turkish ? "Nöbet Eklendi" : "Shift Created",
            ActivityType.ShiftUpdated => turkish ? "Nöbet Güncellendi" : "Shift Updated",
            ActivityType.ShiftDeleted => turkish ? "Nöbet Silindi" : "Shift Deleted",
            ActivityType.ShiftBulkCreated => turkish ? "Toplu Nöbet Eklendi" : "Bulk Shifts Created",
            ActivityType.ShiftBulkDeleted => turkish ? "Toplu Nöbet Silindi" : "Bulk Shifts Deleted",
            
            // İzin
            ActivityType.LeaveCreated => turkish ? "İzin Eklendi" : "Leave Created",
            ActivityType.LeaveUpdated => turkish ? "İzin Güncellendi" : "Leave Updated",
            ActivityType.LeaveDeleted => turkish ? "İzin Silindi" : "Leave Deleted",
            
            // Birim
            ActivityType.UnitCreated => turkish ? "Birim Eklendi" : "Unit Created",
            ActivityType.UnitUpdated => turkish ? "Birim Güncellendi" : "Unit Updated",
            ActivityType.UnitDeleted => turkish ? "Birim Silindi" : "Unit Deleted",
            
            // Birim Tipi
            ActivityType.UnitTypeCreated => turkish ? "Birim Tipi Eklendi" : "Unit Type Created",
            ActivityType.UnitTypeUpdated => turkish ? "Birim Tipi Güncellendi" : "Unit Type Updated",
            ActivityType.UnitTypeDeleted => turkish ? "Birim Tipi Silindi" : "Unit Type Deleted",
            
            // Vardiya Şablonu
            ActivityType.ShiftTemplateCreated => turkish ? "Vardiya Şablonu Eklendi" : "Shift Template Created",
            ActivityType.ShiftTemplateUpdated => turkish ? "Vardiya Şablonu Güncellendi" : "Shift Template Updated",
            ActivityType.ShiftTemplateDeleted => turkish ? "Vardiya Şablonu Silindi" : "Shift Template Deleted",
            
            // Puantaj
            ActivityType.PayrollCreated => turkish ? "Puantaj Kaydedildi" : "Payroll Created",
            ActivityType.PayrollDeleted => turkish ? "Puantaj Silindi" : "Payroll Deleted",
            ActivityType.PayrollExported => turkish ? "Puantaj Dışa Aktarıldı" : "Payroll Exported",
            
            // Mesai
            ActivityType.AttendanceRecordCreated => turkish ? "Mesai Kaydı Eklendi" : "Attendance Record Created",
            ActivityType.AttendanceRecordUpdated => turkish ? "Mesai Kaydı Güncellendi" : "Attendance Record Updated",
            ActivityType.AttendanceRecordDeleted => turkish ? "Mesai Kaydı Silindi" : "Attendance Record Deleted",
            ActivityType.AttendanceApiUsed => turkish ? "Mesai API Kullanıldı" : "Attendance API Used",
            
            // API
            ActivityType.ApiCredentialCreated => turkish ? "API Kimliği Oluşturuldu" : "API Credential Created",
            ActivityType.ApiCredentialUpdated => turkish ? "API Kimliği Güncellendi" : "API Credential Updated",
            ActivityType.ApiCredentialDeleted => turkish ? "API Kimliği Silindi" : "API Credential Deleted",
            ActivityType.ApiCredentialToggled => turkish ? "API Durumu Değiştirildi" : "API Credential Toggled",
            
            // Temizlik Çizelgesi
            ActivityType.CleaningScheduleCreated => turkish ? "Temizlik Çizelgesi Eklendi" : "Cleaning Schedule Created",
            ActivityType.CleaningScheduleUpdated => turkish ? "Temizlik Çizelgesi Güncellendi" : "Cleaning Schedule Updated",
            ActivityType.CleaningScheduleDeleted => turkish ? "Temizlik Çizelgesi Silindi" : "Cleaning Schedule Deleted",
            ActivityType.CleaningItemCreated => turkish ? "Temizlik Maddesi Eklendi" : "Cleaning Item Created",
            ActivityType.CleaningItemUpdated => turkish ? "Temizlik Maddesi Güncellendi" : "Cleaning Item Updated",
            ActivityType.CleaningItemDeleted => turkish ? "Temizlik Maddesi Silindi" : "Cleaning Item Deleted",
            ActivityType.CleaningRecordApproved => turkish ? "Temizlik Kaydı Onaylandı" : "Cleaning Record Approved",
            ActivityType.CleaningRecordRejected => turkish ? "Temizlik Kaydı Reddedildi" : "Cleaning Record Rejected",
            ActivityType.CleaningQrAccessed => turkish ? "QR Kod ile Erişim" : "QR Code Accessed",
            
            // Kullanıcı
            ActivityType.UserLoggedIn => turkish ? "Giriş Yapıldı" : "User Logged In",
            ActivityType.UserLoggedOut => turkish ? "Çıkış Yapıldı" : "User Logged Out",
            ActivityType.UserSettingsUpdated => turkish ? "Ayarlar Güncellendi" : "Settings Updated",
            ActivityType.OrganizationUpdated => turkish ? "Organizasyon Güncellendi" : "Organization Updated",
            
            // Admin
            ActivityType.AdminUserUpdated => turkish ? "Kullanıcı Güncellendi (Admin)" : "User Updated (Admin)",
            ActivityType.AdminUserCreated => turkish ? "Kullanıcı Oluşturuldu (Admin)" : "User Created (Admin)",
            ActivityType.AdminUserDeleted => turkish ? "Kullanıcı Silindi (Admin)" : "User Deleted (Admin)",
            
            _ => turkish ? "Diğer İşlem" : "Other Action"
        };
    }

    public static string GetIcon(this ActivityType type)
    {
        return type switch
        {
            // Personel
            ActivityType.EmployeeCreated or ActivityType.EmployeeUpdated or ActivityType.EmployeeDeleted => "👤",
            
            // Nöbet
            ActivityType.ShiftCreated or ActivityType.ShiftUpdated or ActivityType.ShiftDeleted or 
            ActivityType.ShiftBulkCreated or ActivityType.ShiftBulkDeleted => "📅",
            
            // İzin
            ActivityType.LeaveCreated or ActivityType.LeaveUpdated or ActivityType.LeaveDeleted => "🏖️",
            
            // Birim
            ActivityType.UnitCreated or ActivityType.UnitUpdated or ActivityType.UnitDeleted => "🏢",
            ActivityType.UnitTypeCreated or ActivityType.UnitTypeUpdated or ActivityType.UnitTypeDeleted => "🏷️",
            
            // Vardiya Şablonu
            ActivityType.ShiftTemplateCreated or ActivityType.ShiftTemplateUpdated or ActivityType.ShiftTemplateDeleted => "⏰",
            
            // Puantaj
            ActivityType.PayrollCreated or ActivityType.PayrollDeleted or ActivityType.PayrollExported => "💰",
            
            // Mesai
            ActivityType.AttendanceRecordCreated or ActivityType.AttendanceRecordUpdated or 
            ActivityType.AttendanceRecordDeleted or ActivityType.AttendanceApiUsed => "⏱️",
            
            // API
            ActivityType.ApiCredentialCreated or ActivityType.ApiCredentialUpdated or 
            ActivityType.ApiCredentialDeleted or ActivityType.ApiCredentialToggled => "🔑",
            
            // Temizlik
            ActivityType.CleaningScheduleCreated or ActivityType.CleaningScheduleUpdated or 
            ActivityType.CleaningScheduleDeleted or ActivityType.CleaningItemCreated or
            ActivityType.CleaningItemUpdated or ActivityType.CleaningItemDeleted or
            ActivityType.CleaningRecordApproved or ActivityType.CleaningRecordRejected or
            ActivityType.CleaningQrAccessed => "🧹",
            
            // Kullanıcı
            ActivityType.UserLoggedIn => "🔓",
            ActivityType.UserLoggedOut => "🔒",
            ActivityType.UserSettingsUpdated or ActivityType.OrganizationUpdated => "⚙️",
            
            // Admin
            ActivityType.AdminUserUpdated or ActivityType.AdminUserCreated or ActivityType.AdminUserDeleted => "👑",
            
            _ => "📝"
        };
    }

    public static string GetColor(this ActivityType type)
    {
        return type switch
        {
            // Ekleme işlemleri - Yeşil
            ActivityType.EmployeeCreated or ActivityType.ShiftCreated or ActivityType.LeaveCreated or
            ActivityType.UnitCreated or ActivityType.UnitTypeCreated or ActivityType.ShiftTemplateCreated or
            ActivityType.PayrollCreated or ActivityType.AttendanceRecordCreated or ActivityType.ApiCredentialCreated or
            ActivityType.CleaningScheduleCreated or ActivityType.CleaningItemCreated or ActivityType.CleaningRecordApproved or
            ActivityType.AdminUserCreated or ActivityType.ShiftBulkCreated => "#22c55e",
            
            // Güncelleme işlemleri - Mavi
            ActivityType.EmployeeUpdated or ActivityType.ShiftUpdated or ActivityType.LeaveUpdated or
            ActivityType.UnitUpdated or ActivityType.UnitTypeUpdated or ActivityType.ShiftTemplateUpdated or
            ActivityType.AttendanceRecordUpdated or ActivityType.ApiCredentialUpdated or ActivityType.ApiCredentialToggled or
            ActivityType.CleaningScheduleUpdated or ActivityType.CleaningItemUpdated or
            ActivityType.AdminUserUpdated or ActivityType.UserSettingsUpdated or ActivityType.OrganizationUpdated => "#3b82f6",
            
            // Silme işlemleri - Kırmızı
            ActivityType.EmployeeDeleted or ActivityType.ShiftDeleted or ActivityType.LeaveDeleted or
            ActivityType.UnitDeleted or ActivityType.UnitTypeDeleted or ActivityType.ShiftTemplateDeleted or
            ActivityType.PayrollDeleted or ActivityType.AttendanceRecordDeleted or ActivityType.ApiCredentialDeleted or
            ActivityType.CleaningScheduleDeleted or ActivityType.CleaningItemDeleted or ActivityType.CleaningRecordRejected or
            ActivityType.AdminUserDeleted or ActivityType.ShiftBulkDeleted => "#ef4444",
            
            // Erişim işlemleri - Mor
            ActivityType.UserLoggedIn or ActivityType.UserLoggedOut or ActivityType.CleaningQrAccessed => "#8b5cf6",
            
            // API / Export - Turuncu
            ActivityType.AttendanceApiUsed or ActivityType.PayrollExported => "#f59e0b",
            
            _ => "#64748b"
        };
    }
}

