using System.ComponentModel.DataAnnotations;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Models;

public class PersonelPuanYonetimViewModel
{
    public List<PersonelNobetPuan> Personeller { get; set; } = new();
    public PersonelPuanInputModel NewPersonel { get; set; } = new();
}

public class PersonelPuanInputModel
{
    public int? Id { get; set; }

    [Required]
    [MaxLength(11)]
    public string TcKimlik { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string AdiSoyadi { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Unvan { get; set; }

    [MaxLength(100)]
    public string? Mezuniyet { get; set; }

    [Range(0, 999)]
    public int YPuan { get; set; } = 100;

    [Range(0, 9999)]
    public decimal NormalSaatUcreti { get; set; }

    [Range(0, 9999)]
    public decimal YogunBakimSaatUcreti { get; set; }

    [Range(0, 9999)]
    public decimal IcapSaatUcreti { get; set; }

    [MaxLength(50)]
    public string? Iban { get; set; }

    [MaxLength(100)]
    public string? OncekiSoyadi { get; set; }

    [MaxLength(255)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
