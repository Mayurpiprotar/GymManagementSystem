using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// Checkbox item helper for displaying specializations to applicants.
/// </summary>
public class SpecializationCheckboxItem
{
    public int SpecializationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSelected { get; set; }
}

/// <summary>
/// Public view model for submitting a new trainer application.
/// Excludes all internal administrative, status, and database-specific metadata.
/// </summary>
public class TrainerApplicationCreateViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Please provide a valid phone number.")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
    [Display(Name = "Phone Number")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Years of experience is required.")]
    [Range(0, 50, ErrorMessage = "Experience must be between 0 and 50 years.")]
    [Display(Name = "Years of Experience")]
    public int ExperienceYears { get; set; }

    [Required(ErrorMessage = "Qualifications summary is required.")]
    [StringLength(500, ErrorMessage = "Qualifications summary cannot exceed 500 characters.")]
    [Display(Name = "Qualifications & Certifications Summary")]
    public string QualificationsSummary { get; set; } = string.Empty;

    [Display(Name = "Specializations")]
    public List<int> SelectedSpecializationIds { get; set; } = new();

    /// <summary>
    /// Populated by the server to render available specializations as checkboxes.
    /// </summary>
    public List<SpecializationCheckboxItem> AvailableSpecializations { get; set; } = new();

    [Required(ErrorMessage = "Certification proof document is required.")]
    [Display(Name = "Certification Document (PDF, JPG, PNG - Max 5MB)")]
    public IFormFile? CertificationDocument { get; set; }

    [Display(Name = "Experience Document (Optional - Max 5MB)")]
    public IFormFile? ExperienceDocument { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
