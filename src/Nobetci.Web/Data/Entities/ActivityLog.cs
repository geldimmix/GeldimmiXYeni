using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Aktivite türleri
/// </summary>
public enum ActivityType
{
    // Personel işlemleri
    EmployeeCreated = 100,
    EmployeeUpdated = 101,
    EmployeeDeleted = 102,
    
    // Nöbet işlemleri
    ShiftCreated = 200,
    ShiftUpdated = 201,
    ShiftDeleted = 202,
    ShiftBulkCreated = 203,
    ShiftBulkDeleted = 204,
    
    // İzin işlemleri
    LeaveCreated = 300,
    LeaveUpdated = 301,
    LeaveDeleted = 302,
    
    // Birim işlemleri
    UnitCreated = 400,
    UnitUpdated = 401,
    UnitDeleted = 402,
    
    // Birim Tipi işlemleri
    UnitTypeCreated = 450,
    UnitTypeUpdated = 451,
    UnitTypeDeleted = 452,
    
    // Vardiya şablonu işlemleri
    ShiftTemplateCreated = 500,
    ShiftTemplateUpdated = 501,
    ShiftTemplateDeleted = 502,
    
    // Puantaj işlemleri
    PayrollCreated = 600,
    PayrollDeleted = 601,
    PayrollExported = 602,
    
    // Mesai (Attendance) işlemleri
    AttendanceRecordCreated = 700,
    AttendanceRecordUpdated = 701,
    AttendanceRecordDeleted = 702,
    AttendanceApiUsed = 703,
    
    // API işlemleri
    ApiCredentialCreated = 800,
    ApiCredentialUpdated = 801,
    ApiCredentialDeleted = 802,
    ApiCredentialToggled = 803,
    
    // Temizlik çizelgesi işlemleri
    CleaningScheduleCreated = 900,
    CleaningScheduleUpdated = 901,
    CleaningScheduleDeleted = 902,
    CleaningItemCreated = 910,
    CleaningItemUpdated = 911,
    CleaningItemDeleted = 912,
    CleaningRecordApproved = 920,
    CleaningRecordRejected = 921,
    CleaningQrAccessed = 930,
    
    // Kullanıcı/Hesap işlemleri
    UserLoggedIn = 1000,
    UserLoggedOut = 1001,
    UserSettingsUpdated = 1002,
    OrganizationUpdated = 1003,
    
    // Admin işlemleri
    AdminUserUpdated = 1100,
    AdminUserCreated = 1101,
    AdminUserDeleted = 1102,
    
    // Diğer
    Other = 9999
}

/// <summary>
/// Aktivite log entity
/// </summary>
public class ActivityLog
{
    [Key]
    public long Id { get; set; }
    
    /// <summary>
    /// İşlemi yapan kullanıcı ID
    /// </summary>
    public string? UserId { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
    
    /// <summary>
    /// Organizasyon ID
    /// </summary>
    public int? OrganizationId { get; set; }
    
    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }
    
    /// <summary>
    /// Aktivite türü
    /// </summary>
    public ActivityType ActivityType { get; set; }
    
    /// <summary>
    /// Entity türü (Employee, Shift, Leave, Unit vb.)
    /// </summary>
    [MaxLength(100)]
    public string? EntityType { get; set; }
    
    /// <summary>
    /// Entity ID
    /// </summary>
    public int? EntityId { get; set; }
    
    /// <summary>
    /// İşlem açıklaması
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Detaylı bilgi (JSON formatında eski/yeni değerler)
    /// </summary>
    public string? Details { get; set; }
    
    /// <summary>
    /// IP adresi
    /// </summary>
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User Agent
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Oluşturulma tarihi
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

