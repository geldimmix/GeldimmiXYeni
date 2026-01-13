using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// CMS Content Page for SEO pages (editable from admin panel)
/// </summary>
public class ContentPage
{
    public int Id { get; set; }
    
    /// <summary>
    /// URL slug (e.g., "nobet-olusturma", "nurse-shift-scheduling")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>
    /// Language code (tr or en)
    /// </summary>
    [Required]
    [MaxLength(5)]
    public string Language { get; set; } = "en";
    
    /// <summary>
    /// Page title (for browser tab and H1)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// Meta description for SEO
    /// </summary>
    [MaxLength(500)]
    public string? MetaDescription { get; set; }
    
    /// <summary>
    /// Meta keywords for SEO
    /// </summary>
    [MaxLength(500)]
    public string? MetaKeywords { get; set; }
    
    /// <summary>
    /// Page content (HTML)
    /// </summary>
    public string? Content { get; set; }
    
    /// <summary>
    /// Hero section subtitle
    /// </summary>
    [MaxLength(300)]
    public string? Subtitle { get; set; }
    
    /// <summary>
    /// Call-to-action button text
    /// </summary>
    [MaxLength(100)]
    public string? CtaText { get; set; }
    
    /// <summary>
    /// Call-to-action button URL
    /// </summary>
    [MaxLength(200)]
    public string? CtaUrl { get; set; }
    
    /// <summary>
    /// Whether the page is published
    /// </summary>
    public bool IsPublished { get; set; } = true;
    
    /// <summary>
    /// Display order for navigation
    /// </summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// Page type for categorization
    /// </summary>
    public PageType PageType { get; set; } = PageType.Feature;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PageType
{
    Landing = 0,    // Ana sayfa
    Feature = 1,    // Özellik sayfası
    Blog = 2,       // Blog yazısı
    Legal = 3       // Yasal sayfa (KVKK, Gizlilik vs)
}


