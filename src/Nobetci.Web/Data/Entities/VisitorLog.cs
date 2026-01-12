using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

public class VisitorLog
{
    public int Id { get; set; }
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    [MaxLength(100)]
    public string? Browser { get; set; }
    
    [MaxLength(100)]
    public string? BrowserVersion { get; set; }
    
    [MaxLength(100)]
    public string? OperatingSystem { get; set; }
    
    [MaxLength(50)]
    public string? DeviceType { get; set; } // Desktop, Mobile, Tablet
    
    [MaxLength(500)]
    public string? PageUrl { get; set; }
    
    [MaxLength(100)]
    public string? PagePath { get; set; }
    
    [MaxLength(500)]
    public string? QueryString { get; set; }
    
    [MaxLength(10)]
    public string? HttpMethod { get; set; }
    
    [MaxLength(500)]
    public string? Referrer { get; set; }
    
    [MaxLength(10)]
    public string? Language { get; set; }
    
    [MaxLength(100)]
    public string? Country { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    public int? StatusCode { get; set; }
    
    public long? ResponseTimeMs { get; set; }
    
    [MaxLength(100)]
    public string? SessionId { get; set; }
    
    [MaxLength(450)]
    public string? UserId { get; set; } // If logged in
    
    [MaxLength(100)]
    public string? Action { get; set; } // Custom action like "CreateShift", "Login", etc.
    
    [MaxLength(1000)]
    public string? ActionDetails { get; set; } // JSON details of the action
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


