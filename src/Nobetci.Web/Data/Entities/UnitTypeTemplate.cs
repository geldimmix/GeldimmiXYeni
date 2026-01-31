using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

public class UnitTypeTemplate
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? NameEn { get; set; }

    public decimal DefaultCoefficient { get; set; } = 1.0m;

    [MaxLength(50)]
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public int SortOrder { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; } = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);

    [Column(TypeName = "timestamp without time zone")]
    public DateTime? UpdatedAt { get; set; }
}
