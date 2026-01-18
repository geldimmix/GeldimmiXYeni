using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Time attendance record - check-in/check-out tracking for employees
/// </summary>
public class TimeAttendance
{
    public int Id { get; set; }
    
    public int EmployeeId { get; set; }
    
    /// <summary>
    /// Date of the attendance record
    /// </summary>
    public DateOnly Date { get; set; }
    
    /// <summary>
    /// Check-in time (giriş saati)
    /// </summary>
    public TimeOnly? CheckInTime { get; set; }
    
    /// <summary>
    /// Check-out time (çıkış saati)
    /// </summary>
    public TimeOnly? CheckOutTime { get; set; }
    
    /// <summary>
    /// Whether check-in spans from previous day (e.g., night shift started yesterday)
    /// </summary>
    public bool CheckInFromPreviousDay { get; set; }
    
    /// <summary>
    /// Whether check-out spans to next day (e.g., night shift ends tomorrow)
    /// </summary>
    public bool CheckOutToNextDay { get; set; }
    
    /// <summary>
    /// Attendance type
    /// </summary>
    public AttendanceType Type { get; set; } = AttendanceType.Normal;
    
    /// <summary>
    /// Source of the record (Manual, API, Device)
    /// </summary>
    public AttendanceSource Source { get; set; } = AttendanceSource.Manual;
    
    /// <summary>
    /// Device or API identifier that created this record
    /// </summary>
    [MaxLength(100)]
    public string? SourceIdentifier { get; set; }
    
    /// <summary>
    /// Optional notes
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    /// <summary>
    /// GPS location at check-in (latitude,longitude)
    /// </summary>
    [MaxLength(50)]
    public string? CheckInLocation { get; set; }
    
    /// <summary>
    /// GPS location at check-out (latitude,longitude)
    /// </summary>
    [MaxLength(50)]
    public string? CheckOutLocation { get; set; }
    
    /// <summary>
    /// Calculated total worked hours
    /// </summary>
    public decimal? WorkedHours { get; set; }
    
    /// <summary>
    /// Is this record approved by manager
    /// </summary>
    public bool IsApproved { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Employee Employee { get; set; } = null!;
}

public enum AttendanceType
{
    Normal = 0,
    Late = 1,         // Geç giriş
    EarlyLeave = 2,   // Erken çıkış
    Overtime = 3,     // Fazla mesai
    Remote = 4        // Uzaktan çalışma
}

public enum AttendanceSource
{
    Manual = 0,       // Manuel giriş (web arayüzü)
    Api = 1,          // API ile giriş
    Device = 2,       // PDKS cihazı
    Mobile = 3        // Mobil uygulama
}

