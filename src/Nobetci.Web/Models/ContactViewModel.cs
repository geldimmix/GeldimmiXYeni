using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Models;

public class ContactViewModel
{
    [Required(ErrorMessage = "Ad Soyad gerekli")]
    [Display(Name = "Ad Soyad")]
    [StringLength(100)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "E-posta gerekli")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Mesaj gerekli")]
    [Display(Name = "Mesaj")]
    [StringLength(2000, MinimumLength = 10)]
    public string Message { get; set; } = "";
}
