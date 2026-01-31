using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

public class BordroSabitleriTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Column(TypeName = "numeric(10,6)")]
    public decimal Value { get; set; }

    [Required]
    [MaxLength(50)]
    public string ValueType { get; set; } = "ORAN";

    [MaxLength(255)]
    public string? Description { get; set; }

    [MaxLength(10)]
    public string? CadreType { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(255)]
    public string? WorkingUnitIds { get; set; }

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; } = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime? UpdatedAt { get; set; }
}
