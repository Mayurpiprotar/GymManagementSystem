namespace GymManagementSystem.Models;

public class WorkoutPlan
{
    public int WorkoutPlanId { get; set; }
    public int MemberId { get; set; }
    public int TrainerId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
