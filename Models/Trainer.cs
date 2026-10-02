using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class Trainer
{
    public int TrainerId { get; set; }

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

    [Required]
    [StringLength(100)]
    public string Specialization { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime HireDate { get; set; }

    [StringLength(50)]
    public string? ReferralCode { get; set; }

    public bool IsVerified { get; set; } = true;

    // Account linkage
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    // Trainer Application linkage (1 -> 0..1)
    public int? ApplicationId { get; set; }
    public TrainerApplication? Application { get; set; }

    // Navigation properties
    public ICollection<TrainerSpecialization> TrainerSpecializations { get; set; } = new List<TrainerSpecialization>();
    public ICollection<Membership> AssignedMemberships { get; set; } = new List<Membership>();
    public ICollection<WorkoutPlan> WorkoutPlans { get; set; } = new List<WorkoutPlan>();
}
