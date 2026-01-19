using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

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
    
    // Password hashing helpers
    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "GeldimmiSalt2026!"));
        return Convert.ToBase64String(hashedBytes);
    }
    
    public bool VerifyPassword(string password)
    {
        return PasswordHash == HashPassword(password);
    }
}

public static class AdminRoles
{
    public const string SuperAdmin = "SuperAdmin"; // Can manage other admins
    public const string Admin = "Admin";           // Full access except admin management
    public const string Viewer = "Viewer";         // Read-only access
}

