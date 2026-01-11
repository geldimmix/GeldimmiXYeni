using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

/// <summary>
/// Leave/Time-off record for an employee
/// </summary>
public class Leave
{
    public int Id { get; set; }
    
    public int EmployeeId { get; set; }
    
    /// <summary>
    /// Leave start date
    /// </summary>
    public DateOnly StartDate { get; set; }
    
    /// <summary>
    /// Leave end date
    /// </summary>
    public DateOnly EndDate { get; set; }
    
    /// <summary>
    /// Type of leave
    /// </summary>
    public LeaveType Type { get; set; } = LeaveType.Annual;
    
    /// <summary>
    /// Optional notes
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Employee Employee { get; set; } = null!;
}

public enum LeaveType
{
    Annual = 0,    // Yıllık İzin
    Sick = 1,      // Hastalık İzni
    Unpaid = 2,    // Ücretsiz İzin
    Other = 3      // Diğer
}

