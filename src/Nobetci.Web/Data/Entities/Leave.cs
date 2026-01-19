using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Leave assignment for an employee (İzin Kaydı)
/// </summary>
public class Leave
{
    public int Id { get; set; }
    
    public int EmployeeId { get; set; }
    
    public int LeaveTypeId { get; set; }
    
    /// <summary>
    /// Leave date
    /// </summary>
    public DateOnly Date { get; set; }
    
    /// <summary>
    /// Optional notes
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Employee Employee { get; set; } = null!;
    public virtual LeaveType LeaveType { get; set; } = null!;
}
