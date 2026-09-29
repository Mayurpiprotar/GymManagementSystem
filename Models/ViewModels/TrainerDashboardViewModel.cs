using GymManagementSystem.Models;

namespace GymManagementSystem.Models.ViewModels;

public class TrainerDashboardViewModel
{
    public int TrainerId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public int AssignedWorkoutPlansCount { get; set; }
    public int UniqueAssignedMembersCount { get; set; }

    public int ActiveAssignedMembersCount { get; set; }
    public List<Membership> AssignedMemberships { get; set; } = new();

    public List<WorkoutPlan> RecentWorkoutPlans { get; set; } = new();
}
