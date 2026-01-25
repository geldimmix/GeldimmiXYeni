namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Temizlik çizelgesi - ana yapı
/// </summary>
public class CleaningSchedule
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// Çizelge adı (örn: "Tuvalet Temizlik Çizelgesi")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Konum/alan bilgisi (örn: "1. Kat Erkek Tuvaleti")
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// QR ile giriş şifresi (basit 4-6 haneli)
    /// </summary>
    public string? AccessCode { get; set; }
    
    /// <summary>
    /// Benzersiz QR erişim kodu (URL'de kullanılır)
    /// </summary>
    public string QrAccessCode { get; set; } = Guid.NewGuid().ToString("N")[..12].ToUpper();
    
    /// <summary>
    /// Temizlik görevlisi adı
    /// </summary>
    public string? CleanerName { get; set; }
    
    /// <summary>
    /// Temizlik görevlisi telefonu
    /// </summary>
    public string? CleanerPhone { get; set; }
    
    /// <summary>
    /// Grup ID (Premium kullanıcılar için)
    /// </summary>
    public int? GroupId { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Organization Organization { get; set; } = null!;
    public CleaningScheduleGroup? Group { get; set; }
    public List<CleaningItem> Items { get; set; } = new();
    public List<CleaningQrAccess> QrAccesses { get; set; } = new();
}

/// <summary>
/// Çizelge grubu (Premium)
/// </summary>
public class CleaningScheduleGroup
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Organization Organization { get; set; } = null!;
    public List<CleaningSchedule> Schedules { get; set; } = new();
}

/// <summary>
/// Temizlik maddesi
/// </summary>
public class CleaningItem
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    
    /// <summary>
    /// Madde adı (örn: "Lavabo temizliği")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Açıklama/talimat
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Frekans tipi
    /// </summary>
    public CleaningFrequency Frequency { get; set; } = CleaningFrequency.Daily;
    
    /// <summary>
    /// Özel frekans için gün sayısı
    /// </summary>
    public int? FrequencyDays { get; set; }
    
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public CleaningSchedule Schedule { get; set; } = null!;
    public List<CleaningRecord> Records { get; set; } = new();
}

public enum CleaningFrequency
{
    Daily = 0,      // Günlük
    Weekly = 1,     // Haftalık
    Monthly = 2,    // Aylık
    Yearly = 3,     // Yıllık
    Custom = 4      // Özel (gün sayısı ile)
}

/// <summary>
/// Temizlik kaydı (görevli işaretlemesi)
/// </summary>
public class CleaningRecord
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    
    /// <summary>
    /// Tamamlandı olarak işaretlenme zamanı
    /// </summary>
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// İşareti yapan kişi adı (QR girişinde)
    /// </summary>
    public string? CompletedByName { get; set; }
    
    /// <summary>
    /// Onay durumu
    /// </summary>
    public CleaningRecordStatus Status { get; set; } = CleaningRecordStatus.Pending;
    
    /// <summary>
    /// Onay/red zamanı
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
    
    /// <summary>
    /// Onaylayan/reddeden kullanıcı ID
    /// </summary>
    public string? ReviewedById { get; set; }
    
    /// <summary>
    /// Red notu
    /// </summary>
    public string? Note { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public CleaningItem Item { get; set; } = null!;
    public ApplicationUser? ReviewedBy { get; set; }
}

public enum CleaningRecordStatus
{
    Pending = 0,    // Onay bekliyor
    Approved = 1,   // Onaylandı
    Rejected = 2    // Reddedildi
}

/// <summary>
/// QR erişim takibi (limit kontrolü için)
/// </summary>
public class CleaningQrAccess
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    
    /// <summary>
    /// Erişim zamanı
    /// </summary>
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// IP adresi (isteğe bağlı)
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Ay (limit takibi için) - format: 2026-01
    /// </summary>
    public string MonthKey { get; set; } = DateTime.UtcNow.ToString("yyyy-MM");
    
    // Navigation
    public CleaningSchedule Schedule { get; set; } = null!;
}

