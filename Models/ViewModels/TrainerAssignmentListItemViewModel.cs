namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// Row item representing an eligible membership on the Admin trainer assignment overview page.
/// </summary>
public class TrainerAssignmentListItemViewModel
{
    public int MembershipId { get; set; }
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CanonicalStatus { get; set; } = string.Empty;
    public int? TrainingGoalSpecializationId { get; set; }
    public string TrainingGoalName { get; set; } = string.Empty;
    public string? ExperienceLevel { get; set; }
    public string? TrainingPreference { get; set; }
    public int? AssignedTrainerId { get; set; }
    public string? AssignedTrainerName { get; set; }
    public bool IsAssigned => AssignedTrainerId.HasValue;
    public TrainerCandidateViewModel? RecommendedTrainer { get; set; }
}
