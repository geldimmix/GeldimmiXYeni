using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Admin panel users (separate from regular application users)
/// </summary>
public class AdminUser
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? FullName { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    /// <summary>
    /// Admin role: SuperAdmin, Admin, Viewer
    /// </summary>
    [MaxLength(20)]
    public string Role { get; set; } = "Admin";
    
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    
    /// <summary>
    /// Hash password using BCrypt (more secure than SHA256 + static salt)
    /// </summary>
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }
    
    /// <summary>
    /// Verify password against stored BCrypt hash
    /// Also supports legacy SHA256 hashes for backward compatibility
    /// </summary>
    public bool VerifyPassword(string password)
    {
        // Try BCrypt first (new format)
        if (PasswordHash.StartsWith("$2"))
        {
            return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
        }
        
        // Fallback to legacy SHA256 hash (for existing admins)
        return PasswordHash == LegacyHashPassword(password);
    }
    
    /// <summary>
    /// Legacy SHA256 hash method for backward compatibility
    /// </summary>
    private static string LegacyHashPassword(string password)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + GetLegacySalt()));
        return Convert.ToBase64String(hashedBytes);
    }
    
    /// <summary>
    /// Get legacy salt from environment or use default (for backward compatibility only)
    /// </summary>
    private static string GetLegacySalt()
    {
        return Environment.GetEnvironmentVariable("ADMIN_PASSWORD_SALT") ?? "GeldimmiSalt2026!";
    }
}

public static class AdminRoles
{
    public const string SuperAdmin = "SuperAdmin"; // Can manage other admins
    public const string Admin = "Admin";           // Full access except admin management
    public const string Viewer = "Viewer";         // Read-only access
}

