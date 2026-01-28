using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// QR Menü Siparişleri
/// </summary>
public class QrMenuOrder
{
    public int Id { get; set; }
    
    [Required]
    public int MenuId { get; set; }
    
    [ForeignKey(nameof(MenuId))]
    public QrMenu Menu { get; set; } = null!;
    
    /// <summary>
    /// Masa (opsiyonel - masasız sipariş de olabilir)
    /// </summary>
    public int? TableId { get; set; }
    
    [ForeignKey(nameof(TableId))]
    public QrMenuTable? Table { get; set; }
    
    /// <summary>
    /// Sipariş numarası (görüntüleme için)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string OrderNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Müşteri adı (opsiyonel)
    /// </summary>
    [MaxLength(100)]
    public string? CustomerName { get; set; }
    
    /// <summary>
    /// Müşteri telefonu (opsiyonel)
    /// </summary>
    [MaxLength(20)]
    public string? CustomerPhone { get; set; }
    
    /// <summary>
    /// Sipariş notu
    /// </summary>
    [MaxLength(500)]
    public string? Note { get; set; }
    
    /// <summary>
    /// Sipariş durumu
    /// </summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    
    /// <summary>
    /// Toplam tutar
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Para birimi
    /// </summary>
    [MaxLength(10)]
    public string Currency { get; set; } = "TRY";
    
    /// <summary>
    /// Sipariş tarihi
    /// </summary>
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Onay tarihi
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }
    
    /// <summary>
    /// Tamamlanma tarihi
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// İptal tarihi
    /// </summary>
    public DateTime? CancelledAt { get; set; }
    
    /// <summary>
    /// İptal sebebi
    /// </summary>
    [MaxLength(500)]
    public string? CancellationReason { get; set; }
    
    // Navigation properties
    public ICollection<QrMenuOrderItem> Items { get; set; } = new List<QrMenuOrderItem>();
}

/// <summary>
/// Sipariş durumları
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Beklemede
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Onaylandı
    /// </summary>
    Confirmed = 1,
    
    /// <summary>
    /// Hazırlanıyor
    /// </summary>
    Preparing = 2,
    
    /// <summary>
    /// Hazır
    /// </summary>
    Ready = 3,
    
    /// <summary>
    /// Teslim edildi
    /// </summary>
    Delivered = 4,
    
    /// <summary>
    /// Tamamlandı
    /// </summary>
    Completed = 5,
    
    /// <summary>
    /// İptal edildi
    /// </summary>
    Cancelled = -1
}

public static class OrderStatusExtensions
{
    public static string GetDisplayNameTr(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "⏳ Beklemede",
        OrderStatus.Confirmed => "✅ Onaylandı",
        OrderStatus.Preparing => "👨‍🍳 Hazırlanıyor",
        OrderStatus.Ready => "🍽️ Hazır",
        OrderStatus.Delivered => "📦 Teslim Edildi",
        OrderStatus.Completed => "✔️ Tamamlandı",
        OrderStatus.Cancelled => "❌ İptal Edildi",
        _ => status.ToString()
    };
    
    public static string GetDisplayNameEn(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "⏳ Pending",
        OrderStatus.Confirmed => "✅ Confirmed",
        OrderStatus.Preparing => "👨‍🍳 Preparing",
        OrderStatus.Ready => "🍽️ Ready",
        OrderStatus.Delivered => "📦 Delivered",
        OrderStatus.Completed => "✔️ Completed",
        OrderStatus.Cancelled => "❌ Cancelled",
        _ => status.ToString()
    };
    
    public static string GetColorClass(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "warning",
        OrderStatus.Confirmed => "info",
        OrderStatus.Preparing => "primary",
        OrderStatus.Ready => "success",
        OrderStatus.Delivered => "success",
        OrderStatus.Completed => "secondary",
        OrderStatus.Cancelled => "danger",
        _ => "secondary"
    };
}

