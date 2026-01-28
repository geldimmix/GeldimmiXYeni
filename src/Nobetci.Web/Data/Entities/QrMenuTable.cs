using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// QR Menü Masaları - Her masanın kendine özel QR kodu
/// </summary>
public class QrMenuTable
{
    public int Id { get; set; }
    
    [Required]
    public int MenuId { get; set; }
    
    [ForeignKey(nameof(MenuId))]
    public QrMenu Menu { get; set; } = null!;
    
    /// <summary>
    /// Masa numarası/adı
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TableNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Masa açıklaması (örn: "Bahçe", "VIP Bölüm")
    /// </summary>
    [MaxLength(100)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Unique QR kodu için slug
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string QrCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Kaç kişilik masa
    /// </summary>
    public int Capacity { get; set; } = 4;
    
    /// <summary>
    /// Aktif mi?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Sıralama
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<QrMenuOrder> Orders { get; set; } = new List<QrMenuOrder>();
}

