using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

public class PersonelNobetPuan
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }

    [Required]
    [MaxLength(11)]
    public string TcKimlik { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AdiSoyadi { get; set; }

    [MaxLength(200)]
    public string? Unvan { get; set; }

    [MaxLength(100)]
    public string? Mezuniyet { get; set; }

    public int YPuan { get; set; } = 100;

    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalSaatUcreti { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimSaatUcreti { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal IcapSaatUcreti { get; set; }

    [MaxLength(50)]
    public string? Iban { get; set; }

    [MaxLength(100)]
    public string? OncekiSoyadi { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    public virtual Organization Organization { get; set; } = null!;
}
