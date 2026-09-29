namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// Detail view model for the Admin Trainer Assignment & Matching page for a specific eligible membership.
/// </summary>
public class TrainerAssignmentDetailViewModel
{
    public int MembershipId { get; set; }
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string MemberPhone { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;
    public decimal PlanPrice { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CanonicalStatus { get; set; } = string.Empty;
    public bool IsPaid { get; set; }

    public int TrainingGoalSpecializationId { get; set; }
    public string TrainingGoalName { get; set; } = string.Empty;
    public string? ExperienceLevel { get; set; }
    public string? TrainingPreference { get; set; }

    public int? CurrentAssignedTrainerId { get; set; }
    public string? CurrentAssignedTrainerName { get; set; }

    public TrainerCandidateViewModel? RecommendedTrainer { get; set; }
    public List<TrainerCandidateViewModel> MatchingTrainers { get; set; } = new();
    public List<TrainerCandidateViewModel> OtherApprovedTrainers { get; set; } = new();

    public bool HasExactMatches => MatchingTrainers.Count > 0;
    public bool HasApprovedTrainers => MatchingTrainers.Count > 0 || OtherApprovedTrainers.Count > 0;
}
