using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// QR Menü Ürünleri
/// </summary>
public class QrMenuItem
{
    public int Id { get; set; }
    
    [Required]
    public int CategoryId { get; set; }
    
    [ForeignKey(nameof(CategoryId))]
    public QrMenuCategory Category { get; set; } = null!;
    
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Fiyat
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    
    /// <summary>
    /// İndirimli fiyat (varsa)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountedPrice { get; set; }
    
    /// <summary>
    /// Ürün resmi (Premium kullanıcılar için)
    /// </summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// Kalori bilgisi
    /// </summary>
    public int? Calories { get; set; }
    
    /// <summary>
    /// Hazırlanma süresi (dakika)
    /// </summary>
    public int? PrepTimeMinutes { get; set; }
    
    /// <summary>
    /// Alerjen bilgisi
    /// </summary>
    [MaxLength(500)]
    public string? Allergens { get; set; }
    
    /// <summary>
    /// Etiketler (Vejetaryen, Vegan, Glutensiz, vb.)
    /// </summary>
    [MaxLength(200)]
    public string? Tags { get; set; }
    
    /// <summary>
    /// Porsiyon bilgisi
    /// </summary>
    [MaxLength(100)]
    public string? PortionSize { get; set; }
    
    /// <summary>
    /// Sıralama
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
    
    /// <summary>
    /// Aktif mi?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Stokta var mı?
    /// </summary>
    public bool InStock { get; set; } = true;
    
    /// <summary>
    /// Öne çıkan ürün mü?
    /// </summary>
    public bool IsFeatured { get; set; } = false;
    
    /// <summary>
    /// Yeni ürün mü?
    /// </summary>
    public bool IsNew { get; set; } = false;
    
    /// <summary>
    /// Popüler ürün mü?
    /// </summary>
    public bool IsPopular { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Ürün etiketleri
/// </summary>
public static class MenuItemTags
{
    public const string Vegetarian = "vegetarian";
    public const string Vegan = "vegan";
    public const string GlutenFree = "gluten-free";
    public const string Spicy = "spicy";
    public const string Halal = "halal";
    public const string Organic = "organic";
    public const string SugarFree = "sugar-free";
    public const string DairyFree = "dairy-free";
    
    public static readonly Dictionary<string, string> AllTr = new()
    {
        { Vegetarian, "🥬 Vejetaryen" },
        { Vegan, "🌱 Vegan" },
        { GlutenFree, "🌾 Glutensiz" },
        { Spicy, "🌶️ Acılı" },
        { Halal, "☪️ Helal" },
        { Organic, "🌿 Organik" },
        { SugarFree, "🚫 Şekersiz" },
        { DairyFree, "🥛 Süt Ürünsüz" }
    };
    
    public static readonly Dictionary<string, string> AllEn = new()
    {
        { Vegetarian, "🥬 Vegetarian" },
        { Vegan, "🌱 Vegan" },
        { GlutenFree, "🌾 Gluten-Free" },
        { Spicy, "🌶️ Spicy" },
        { Halal, "☪️ Halal" },
        { Organic, "🌿 Organic" },
        { SugarFree, "🚫 Sugar-Free" },
        { DairyFree, "🥛 Dairy-Free" }
    };
}

