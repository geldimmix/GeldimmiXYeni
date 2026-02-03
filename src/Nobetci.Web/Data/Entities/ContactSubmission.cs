using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Contact form submissions from the website (stored for admin view, email also sent via Resend).
/// </summary>
public class ContactSubmission
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Admin can mark as read/processed
    /// </summary>
    public bool IsRead { get; set; }
}
