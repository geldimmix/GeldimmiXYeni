using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// QR Menü Kategorileri - Nested 1 seviye destekler
/// </summary>
public class QrMenuCategory
{
    public int Id { get; set; }
    
    [Required]
    public int MenuId { get; set; }
    
    [ForeignKey(nameof(MenuId))]
    public QrMenu Menu { get; set; } = null!;
    
    /// <summary>
    /// Üst kategori ID (null ise ana kategori)
    /// </summary>
    public int? ParentCategoryId { get; set; }
    
    [ForeignKey(nameof(ParentCategoryId))]
    public QrMenuCategory? ParentCategory { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Kategori ikonu (emoji veya icon class)
    /// </summary>
    [MaxLength(50)]
    public string? Icon { get; set; }
    
    /// <summary>
    /// Kategori resmi URL
    /// </summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Sıralama
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
    
    /// <summary>
    /// Aktif mi?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<QrMenuCategory> SubCategories { get; set; } = new List<QrMenuCategory>();
    public ICollection<QrMenuItem> Items { get; set; } = new List<QrMenuItem>();
}

