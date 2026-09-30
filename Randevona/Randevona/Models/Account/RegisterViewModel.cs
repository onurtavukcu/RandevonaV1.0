using System.ComponentModel.DataAnnotations;

namespace Randevona.Models.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Adınızı girin.")]
    [StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir.")]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyadınızı girin.")]
    [StringLength(100, ErrorMessage = "Soyad en fazla 100 karakter olabilir.")]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "İşletme adını girin.")]
    [StringLength(200, ErrorMessage = "İşletme adı en fazla 200 karakter olabilir.")]
    [Display(Name = "İşletme adı")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "İlk şubenizin adını girin.")]
    [StringLength(200, ErrorMessage = "Şube adı en fazla 200 karakter olabilir.")]
    [Display(Name = "Şube adı")]
    public string OrganizationName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta adresinizi girin.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(254, ErrorMessage = "E-posta en fazla 254 karakter olabilir.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bir şifre belirleyin.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifrenizi tekrar girin.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    [Display(Name = "Şifre tekrarı")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
