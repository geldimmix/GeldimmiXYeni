using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nobetci.Web.Data.Entities;

[Table("BordroResult4A")]
public class BordroResult4A
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int NobetPuani { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal SaatUcreti { get; set; }
    public bool YogunBakimVar { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalServisNobetSaati { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimNobetSaati { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalServisBayramSaati { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimBayramSaati { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal BayramFarkiNobetSaati { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalServisNobetToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimNobetToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalServisBayramToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimBayramToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal BayramFarkiToplamTutar { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal GenelToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal DamgaVergisi { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal EleGecenToplam { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Organization Organization { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
}

[Table("BordroResult4B")]
public class BordroResult4B
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int NobetPuani { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal SaatUcreti { get; set; }
    public bool YogunBakimVar { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalServisNobetSaati { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimNobetSaati { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalServisBayramSaati { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimBayramSaati { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal BayramFarkiNobetSaati { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalServisNobetToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimNobetToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal NormalServisBayramToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal YogunBakimBayramToplamTutar { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal BayramFarkiToplamTutar { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal GenelToplamTutarPek { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal MaluliyetYaslilikEmeklilikDev { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal GssDev { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal KisaVadSigKolPrim { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal GelirToplami { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal DamgaVergisi { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal MaluliyetYaslilikEmeklilikDevKesinti { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal GssDevKesinti { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal KisaVadSigKolPrimKesinti { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal MaluliyetYaslilikEmeklilikKisi { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal GssKisi { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal KesintiToplami { get; set; }
    [Column(TypeName = "numeric(10,2)")]
    public decimal EleGecenToplam { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Organization Organization { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
}
