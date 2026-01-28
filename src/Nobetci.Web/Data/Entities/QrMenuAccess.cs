using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// QR Menü Erişim Takibi - Günlük limit kontrolü için
/// </summary>
public class QrMenuAccess
{
    public int Id { get; set; }
    
    [Required]
    public int MenuId { get; set; }
    
    [ForeignKey(nameof(MenuId))]
    public QrMenu Menu { get; set; } = null!;
    
    /// <summary>
    /// Masa ID (opsiyonel)
    /// </summary>
    public int? TableId { get; set; }
    
    [ForeignKey(nameof(TableId))]
    public QrMenuTable? Table { get; set; }
    
    /// <summary>
    /// Ziyaretçi IP adresi
    /// </summary>
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// User Agent
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Session ID
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Erişim tarihi
    /// </summary>
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Sadece tarih (limit kontrolü için index)
    /// </summary>
    public DateOnly AccessDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}

