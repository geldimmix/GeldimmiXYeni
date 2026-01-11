using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email gerekli")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi girin")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre gerekli")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Ad soyad gerekli")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email gerekli")]
    [EmailAddress(ErrorMessage = "Geçerli bir email adresi girin")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre gerekli")]
    [StringLength(100, ErrorMessage = "Şifre en az {2} karakter olmalı", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Şifre tekrar")]
    [Compare("Password", ErrorMessage = "Şifreler eşleşmiyor")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

