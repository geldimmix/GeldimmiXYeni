using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// API Key for external integrations - used for JWT token generation
/// </summary>
public class ApiKey
{
    public int Id { get; set; }
    
    public int OrganizationId { get; set; }
    
    /// <summary>
    /// Unique API Key (hashed for security)
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;
    
    /// <summary>
    /// API Key prefix for display (first 8 chars)
    /// </summary>
    [Required]
    [MaxLength(12)]
    public string KeyPrefix { get; set; } = string.Empty;
    
    /// <summary>
    /// Friendly name for the API key
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what this key is used for
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Permissions granted to this key (comma-separated)
    /// e.g., "attendance:read,attendance:write"
    /// </summary>
    [MaxLength(500)]
    public string Permissions { get; set; } = "attendance:write";
    
    /// <summary>
    /// IP whitelist (comma-separated, empty = all allowed)
    /// </summary>
    [MaxLength(500)]
    public string? IpWhitelist { get; set; }
    
    /// <summary>
    /// Whether the key is active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Expiration date (null = never expires)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// Last time this key was used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
    
    /// <summary>
    /// Number of times this key was used
    /// </summary>
    public int UsageCount { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public virtual Organization Organization { get; set; } = null!;
}

