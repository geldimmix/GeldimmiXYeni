using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Data.Entities;

public class BordroYetkileri
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int UnitId { get; set; }

    [Required]
    [MaxLength(20)]
    public string TcKimlik { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? KadroTipiYetkisi { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;
    public virtual Unit Unit { get; set; } = null!;
}
