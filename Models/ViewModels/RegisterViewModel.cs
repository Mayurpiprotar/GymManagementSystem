using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GymManagementSystem.Models.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please select your role.")]
    [Display(Name = "Register As")]
    public string Role { get; set; } = "Member"; // "Member" or "Trainer"

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid Email Address.")]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contact number is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [Display(Name = "Contact Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Referral Code (Optional)")]
    [StringLength(50)]
    public string? ReferralCode { get; set; }

    // Trainer-Specific Registration Fields
    [Display(Name = "Years of Professional Experience")]
    [Range(0, 40, ErrorMessage = "Experience must be between 0 and 40 years.")]
    public int? ExperienceYears { get; set; }

    [Display(Name = "Qualifications & Certifications Summary")]
    [StringLength(500, ErrorMessage = "Qualifications cannot exceed 500 characters.")]
    public string? QualificationsSummary { get; set; }

    [Display(Name = "Specializations")]
    public List<int> SelectedSpecializationIds { get; set; } = new();

    public List<SelectListItem> AvailableSpecializations { get; set; } = new();

    [Display(Name = "Certification / Experience Proof Document (PDF, PNG, JPG)")]
    public IFormFile? CertificationDocument { get; set; }
}
