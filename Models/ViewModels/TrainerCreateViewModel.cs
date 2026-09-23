using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models.ViewModels;

public class TrainerCreateViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email Address is required.")]
    [EmailAddress(ErrorMessage = "Invalid Email Address.")]
    [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone Number is required.")]
    [Phone(ErrorMessage = "Invalid Phone Number.")]
    [StringLength(20, ErrorMessage = "Phone Number cannot exceed 20 characters.")]
    [Display(Name = "Phone Number")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Specialization is required.")]
    [StringLength(100, ErrorMessage = "Specialization cannot exceed 100 characters.")]
    public string Specialization { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hire Date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Hire Date")]
    public DateTime HireDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
