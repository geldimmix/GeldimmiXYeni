using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Blog post entity for SEO blog articles (managed from admin panel)
/// </summary>
public class BlogPost
{
    public int Id { get; set; }
    
    /// <summary>
    /// URL slug (e.g., "hemsire-nobet-listesi-nasil-hazirlanir")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Slug { get; set; } = string.Empty;
    
    // ========== TURKISH CONTENT ==========
    
    /// <summary>
    /// Turkish title
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string TitleTr { get; set; } = string.Empty;
    
    /// <summary>
    /// Turkish excerpt/summary for listing pages
    /// </summary>
    [MaxLength(500)]
    public string? ExcerptTr { get; set; }
    
    /// <summary>
    /// Turkish content (HTML)
    /// </summary>
    public string ContentTr { get; set; } = string.Empty;
    
    /// <summary>
    /// Turkish SEO keywords (comma-separated)
    /// </summary>
    [MaxLength(500)]
    public string? KeywordsTr { get; set; }
    
    /// <summary>
    /// Turkish meta description
    /// </summary>
    [MaxLength(500)]
    public string? MetaDescriptionTr { get; set; }
    
    // ========== ENGLISH CONTENT ==========
    
    /// <summary>
    /// English title
    /// </summary>
    [MaxLength(300)]
    public string? TitleEn { get; set; }
    
    /// <summary>
    /// English excerpt/summary for listing pages
    /// </summary>
    [MaxLength(500)]
    public string? ExcerptEn { get; set; }
    
    /// <summary>
    /// English content (HTML)
    /// </summary>
    public string? ContentEn { get; set; }
    
    /// <summary>
    /// English SEO keywords (comma-separated)
    /// </summary>
    [MaxLength(500)]
    public string? KeywordsEn { get; set; }
    
    /// <summary>
    /// English meta description
    /// </summary>
    [MaxLength(500)]
    public string? MetaDescriptionEn { get; set; }
    
    // ========== SEO & META ==========
    
    /// <summary>
    /// Open Graph image URL for social sharing
    /// </summary>
    [MaxLength(500)]
    public string? OgImageUrl { get; set; }
    
    /// <summary>
    /// Canonical URL (if different from default)
    /// </summary>
    [MaxLength(500)]
    public string? CanonicalUrl { get; set; }
    
    /// <summary>
    /// Schema.org structured data (JSON-LD)
    /// </summary>
    public string? SchemaJson { get; set; }
    
    /// <summary>
    /// Robots meta tag (index, follow, noindex, nofollow)
    /// </summary>
    [MaxLength(50)]
    public string? RobotsMeta { get; set; }
    
    // ========== PUBLISHING ==========
    
    /// <summary>
    /// Whether the post is published
    /// </summary>
    public bool IsPublished { get; set; } = true;
    
    /// <summary>
    /// Whether the post is featured (shown prominently)
    /// </summary>
    public bool IsFeatured { get; set; } = false;
    
    /// <summary>
    /// Display order (lower = first)
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
    
    /// <summary>
    /// Publication date
    /// </summary>
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Author name (optional)
    /// </summary>
    [MaxLength(100)]
    public string? AuthorName { get; set; }
    
    /// <summary>
    /// View count for analytics
    /// </summary>
    public int ViewCount { get; set; } = 0;
    
    // ========== HELPERS ==========
    
    /// <summary>
    /// Get title based on language
    /// </summary>
    public string GetTitle(bool isTurkish) => isTurkish ? TitleTr : (TitleEn ?? TitleTr);
    
    /// <summary>
    /// Get excerpt based on language
    /// </summary>
    public string GetExcerpt(bool isTurkish) => isTurkish ? (ExcerptTr ?? "") : (ExcerptEn ?? ExcerptTr ?? "");
    
    /// <summary>
    /// Get content based on language
    /// </summary>
    public string GetContent(bool isTurkish) => isTurkish ? ContentTr : (ContentEn ?? ContentTr);
    
    /// <summary>
    /// Get keywords as array based on language
    /// </summary>
    public string[] GetKeywords(bool isTurkish)
    {
        var keywords = isTurkish ? KeywordsTr : (KeywordsEn ?? KeywordsTr);
        return string.IsNullOrEmpty(keywords) 
            ? Array.Empty<string>() 
            : keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
    
    /// <summary>
    /// Get meta description based on language
    /// </summary>
    public string GetMetaDescription(bool isTurkish) => 
        isTurkish ? (MetaDescriptionTr ?? ExcerptTr ?? "") : (MetaDescriptionEn ?? ExcerptEn ?? MetaDescriptionTr ?? ExcerptTr ?? "");
}

