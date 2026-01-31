using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

public class BordroSabitleri
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }

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

    [Column(TypeName = "timestamp without time zone")]
    public DateTime ValidFrom { get; set; } = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, 0, 0, 0, DateTimeKind.Unspecified);

    [Column(TypeName = "timestamp without time zone")]
    public DateTime? ValidTo { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(255)]
    public string? WorkingUnitIds { get; set; }

    public int? TemplateId { get; set; }

    public bool IsCustom { get; set; } = false;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; } = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime? UpdatedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;
}

public class BordroSabitleriGecmis
{
    public int Id { get; set; }

    public int OrganizationId { get; set; }

    public int SabitId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Column(TypeName = "numeric(10,6)")]
    public decimal OldValue { get; set; }

    [Column(TypeName = "numeric(10,6)")]
    public decimal NewValue { get; set; }

    [MaxLength(50)]
    public string ValueType { get; set; } = "ORAN";

    [MaxLength(255)]
    public string? Description { get; set; }

    [MaxLength(10)]
    public string? CadreType { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime? OldValidFrom { get; set; }
    [Column(TypeName = "timestamp without time zone")]
    public DateTime? OldValidTo { get; set; }
    [Column(TypeName = "timestamp without time zone")]
    public DateTime? NewValidFrom { get; set; }
    [Column(TypeName = "timestamp without time zone")]
    public DateTime? NewValidTo { get; set; }

    [MaxLength(255)]
    public string? OldWorkingUnitIds { get; set; }

    [MaxLength(255)]
    public string? NewWorkingUnitIds { get; set; }

    [MaxLength(20)]
    public string ActionType { get; set; } = "UPDATE";

    [MaxLength(100)]
    public string? ActionBy { get; set; }

    [Column(TypeName = "timestamp without time zone")]
    public DateTime ActionAt { get; set; } = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified);

    public virtual Organization Organization { get; set; } = null!;
}
