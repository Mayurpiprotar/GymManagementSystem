using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class TrainerApplication
{
    public int TrainerApplicationId { get; set; }

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    [StringLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Range(0, 50)]
    public int ExperienceYears { get; set; }

    [Required]
    [StringLength(500)]
    public string QualificationsSummary { get; set; } = string.Empty;

    // Secure temporary password hash created via IPasswordHasher<ApplicationUser>
    // Cleared permanently upon approval or rejection
    public string? TemporaryPasswordHash { get; set; }

    [Required]
    [StringLength(260)]
    public string CertificationDocumentPath { get; set; } = string.Empty;

    [Required]
    [StringLength(260)]
    public string CertificationOriginalFileName { get; set; } = string.Empty;

    [StringLength(260)]
    public string? ExperienceDocumentPath { get; set; }

    [StringLength(260)]
    public string? ExperienceOriginalFileName { get; set; }

    [DataType(DataType.Date)]
    public DateTime AppliedDate { get; set; } = DateTime.Today;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

    [StringLength(500)]
    public string? AdminNotes { get; set; }

    public DateTime? ReviewedDate { get; set; }

    public string? ReviewedByAdminId { get; set; }
    public ApplicationUser? ReviewedByAdmin { get; set; }

    // Navigation to the Trainer profile created upon approval (1 -> 0..1)
    public Trainer? CreatedTrainer { get; set; }

    // Requested specializations
    public ICollection<TrainerApplicationSpecialization> ApplicationSpecializations { get; set; } = new List<TrainerApplicationSpecialization>();
}
