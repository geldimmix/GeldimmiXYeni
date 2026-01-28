using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// QR Menü Sipariş Kalemleri
/// </summary>
public class QrMenuOrderItem
{
    public int Id { get; set; }
    
    [Required]
    public int OrderId { get; set; }
    
    [ForeignKey(nameof(OrderId))]
    public QrMenuOrder Order { get; set; } = null!;
    
    [Required]
    public int MenuItemId { get; set; }
    
    [ForeignKey(nameof(MenuItemId))]
    public QrMenuItem MenuItem { get; set; } = null!;
    
    /// <summary>
    /// Ürün adı (sipariş anındaki)
    /// </summary>
    [Required]
    [MaxLength(150)]
    public string ItemName { get; set; } = string.Empty;
    
    /// <summary>
    /// Adet
    /// </summary>
    public int Quantity { get; set; } = 1;
    
    /// <summary>
    /// Birim fiyat (sipariş anındaki)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }
    
    /// <summary>
    /// Toplam fiyat (Quantity * UnitPrice)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }
    
    /// <summary>
    /// Ürün notu (örn: "Az pişmiş", "Soğansız")
    /// </summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}

