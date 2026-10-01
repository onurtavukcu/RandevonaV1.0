using System.ComponentModel.DataAnnotations;

namespace Randevona.Models.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your first name.")]
    [StringLength(100, ErrorMessage = "First name must not exceed 100 characters.")]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your last name.")]
    [StringLength(100, ErrorMessage = "Last name must not exceed 100 characters.")]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your business name.")]
    [StringLength(200, ErrorMessage = "Business name must not exceed 200 characters.")]
    [Display(Name = "Business name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your first branch name.")]
    [StringLength(200, ErrorMessage = "Branch name must not exceed 200 characters.")]
    [Display(Name = "Branch name")]
    public string OrganizationName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254, ErrorMessage = "Email address must not exceed 254 characters.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

