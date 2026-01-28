using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// QR Menü ana entity - Restoran menüsü
/// </summary>
public class QrMenu
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = "TRY";
    
    [Required]
    [MaxLength(10)]
    public string Language { get; set; } = "tr";
    
    [MaxLength(100)]
    public string? RestaurantName { get; set; }
    
    [MaxLength(500)]
    public string? RestaurantAddress { get; set; }
    
    [MaxLength(20)]
    public string? RestaurantPhone { get; set; }
    
    [MaxLength(200)]
    public string? LogoUrl { get; set; }
    
    [MaxLength(7)]
    public string PrimaryColor { get; set; } = "#E53935";
    
    [MaxLength(7)]
    public string SecondaryColor { get; set; } = "#1E1E1E";
    
    /// <summary>
    /// Menü aktif mi?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Sipariş alma aktif mi?
    /// </summary>
    public bool AcceptOrders { get; set; } = false;
    
    /// <summary>
    /// Unique slug for URL
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>
    /// Oturum ID (kayıtsız kullanıcılar için)
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Kayıtlı kullanıcı ID
    /// </summary>
    [MaxLength(450)]
    public string? UserId { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<QrMenuCategory> Categories { get; set; } = new List<QrMenuCategory>();
    public ICollection<QrMenuTable> Tables { get; set; } = new List<QrMenuTable>();
    public ICollection<QrMenuOrder> Orders { get; set; } = new List<QrMenuOrder>();
    public ICollection<QrMenuAccess> Accesses { get; set; } = new List<QrMenuAccess>();
}

/// <summary>
/// Desteklenen para birimleri
/// </summary>
public static class MenuCurrencies
{
    public static readonly Dictionary<string, string> All = new()
    {
        { "TRY", "₺ Türk Lirası" },
        { "USD", "$ US Dollar" },
        { "EUR", "€ Euro" },
        { "GBP", "£ British Pound" },
        { "JPY", "¥ Japanese Yen" },
        { "CNY", "¥ Chinese Yuan" },
        { "CHF", "CHF Swiss Franc" },
        { "AUD", "A$ Australian Dollar" },
        { "CAD", "C$ Canadian Dollar" },
        { "HKD", "HK$ Hong Kong Dollar" },
        { "SGD", "S$ Singapore Dollar" },
        { "SEK", "kr Swedish Krona" },
        { "NOK", "kr Norwegian Krone" },
        { "DKK", "kr Danish Krone" },
        { "NZD", "NZ$ New Zealand Dollar" },
        { "MXN", "$ Mexican Peso" },
        { "ZAR", "R South African Rand" },
        { "BRL", "R$ Brazilian Real" },
        { "INR", "₹ Indian Rupee" },
        { "KRW", "₩ South Korean Won" },
        { "AED", "د.إ UAE Dirham" }
    };
    
    public static string GetSymbol(string code) => code switch
    {
        "TRY" => "₺",
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" => "¥",
        "CNY" => "¥",
        "CHF" => "CHF",
        "AUD" => "A$",
        "CAD" => "C$",
        "HKD" => "HK$",
        "SGD" => "S$",
        "SEK" => "kr",
        "NOK" => "kr",
        "DKK" => "kr",
        "NZD" => "NZ$",
        "MXN" => "$",
        "ZAR" => "R",
        "BRL" => "R$",
        "INR" => "₹",
        "KRW" => "₩",
        "AED" => "د.إ",
        _ => code
    };
}

/// <summary>
/// Desteklenen diller
/// </summary>
public static class MenuLanguages
{
    public static readonly Dictionary<string, string> All = new()
    {
        { "tr", "Türkçe" },
        { "en", "English" },
        { "de", "Deutsch" },
        { "fr", "Français" },
        { "es", "Español" },
        { "ar", "العربية" },
        { "ru", "Русский" },
        { "zh", "中文" },
        { "ja", "日本語" },
        { "ko", "한국어" }
    };
}

