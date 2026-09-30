using GymManagementSystem.Models;

namespace GymManagementSystem.Models.ViewModels;

public class TrainerMemberDetailsViewModel
{
    public Member Member { get; set; } = null!;
    public Membership CurrentMembership { get; set; } = null!;
    public List<WorkoutPlan> WorkoutPlans { get; set; } = new();

    public int MemberId => Member.MemberId;
    public string MemberName => Member.FullName;
    public string Email => Member.Email;
    public string Phone => Member.Phone;
    public string Gender => Member.Gender;
    public DateTime JoinDate => Member.JoinDate;

    public string PlanName => CurrentMembership.MembershipPlan?.Name ?? "Membership Plan";
    public string GoalName => CurrentMembership.TrainingGoalSpecialization?.Name ?? "General Fitness";
    public string ExperienceLevel => CurrentMembership.ExperienceLevel ?? "Beginner";
    public string TrainingPreference => string.IsNullOrWhiteSpace(CurrentMembership.TrainingPreference)
        ? "No specific workout preference provided"
        : CurrentMembership.TrainingPreference;
    public DateTime StartDate => CurrentMembership.StartDate;
    public DateTime EndDate => CurrentMembership.EndDate;
    public string Status => CurrentMembership.Status;
}
