using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid Email Address.")]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
