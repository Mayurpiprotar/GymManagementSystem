using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class Membership
{
    public int MembershipId { get; set; }

    public int MemberId { get; set; }

    public int MembershipPlanId { get; set; }

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    // Structured Training Preferences
    public int? TrainingGoalSpecializationId { get; set; }
    public Specialization? TrainingGoalSpecialization { get; set; }

    [StringLength(50)]
    public string? ExperienceLevel { get; set; }

    [StringLength(250)]
    public string? TrainingPreference { get; set; }

    // Trainer Assignment
    public int? AssignedTrainerId { get; set; }
    public Trainer? AssignedTrainer { get; set; }

    // Navigation properties
    public Member? Member { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
