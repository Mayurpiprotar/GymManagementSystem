using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class WorkoutPlan
{
    public int WorkoutPlanId { get; set; }

    public int MemberId { get; set; }

    public int TrainerId { get; set; }

    [Required]
    [StringLength(100)]
    public string PlanName { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime CreatedDate { get; set; }

    // Navigation properties
    public Member? Member { get; set; }
    public Trainer? Trainer { get; set; }
}
