using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Ana modül tanımı (örn: Hemşire Nöbet Sistemi, Hasta Takip Sistemi)
/// </summary>
public class Module
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Description { get; set; }
    
    /// <summary>
    /// URL'de ve kodda kullanılacak benzersiz kod (örn: "nurse-shift", "patient-tracking")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Modül ikonu (emoji veya icon class)
    /// </summary>
    [MaxLength(50)]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Modül rengi (hex)
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }
    
    /// <summary>
    /// Sıralama
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    /// <summary>
    /// Aktif mi?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Sistem modülü mü? (silinemez)
    /// </summary>
    public bool IsSystem { get; set; } = false;
    
    /// <summary>
    /// Premium özellik mi?
    /// </summary>
    public bool IsPremium { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public virtual ICollection<SubModule> SubModules { get; set; } = new List<SubModule>();
    public virtual ICollection<UserModuleAccess> UserAccesses { get; set; } = new List<UserModuleAccess>();
}

/// <summary>
/// Alt modül tanımı (örn: Nöbet Yönetimi, Mesai Takip, Puantaj)
/// </summary>
public class SubModule
{
    public int Id { get; set; }
    
    public int ModuleId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Description { get; set; }
    
    /// <summary>
    /// URL'de ve kodda kullanılacak benzersiz kod (örn: "shifts", "attendance", "timesheet")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Alt modül ikonu
    /// </summary>
    [MaxLength(50)]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Yönlendirme URL'i (örn: "/app", "/app/attendance")
    /// </summary>
    [MaxLength(200)]
    public string? RouteUrl { get; set; }
    
    /// <summary>
    /// Sıralama
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    /// <summary>
    /// Aktif mi?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Sistem alt modülü mü? (silinemez)
    /// </summary>
    public bool IsSystem { get; set; } = false;
    
    /// <summary>
    /// Premium özellik mi?
    /// </summary>
    public bool IsPremium { get; set; } = false;
    
    /// <summary>
    /// Bu alt modül için özel erişim kontrolü gerekiyor mu?
    /// </summary>
    public string? RequiredPermission { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public virtual Module Module { get; set; } = null!;
}

/// <summary>
/// Kullanıcının modül erişim hakları
/// </summary>
public class UserModuleAccess
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public int ModuleId { get; set; }
    
    /// <summary>
    /// Erişim izni var mı?
    /// </summary>
    public bool HasAccess { get; set; } = true;
    
    /// <summary>
    /// Erişim başlangıç tarihi (null = hemen)
    /// </summary>
    public DateTime? AccessStartDate { get; set; }
    
    /// <summary>
    /// Erişim bitiş tarihi (null = süresiz)
    /// </summary>
    public DateTime? AccessEndDate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Module Module { get; set; } = null!;
}

