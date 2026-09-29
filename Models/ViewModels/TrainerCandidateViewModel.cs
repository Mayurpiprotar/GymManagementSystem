namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// Presentation model for a candidate trainer in the matching and assignment workflow.
/// Strictly includes only non-sensitive trainer profile and workload details.
/// Passwords, internal security tokens, and verification documents are never exposed.
/// </summary>
public class TrainerCandidateViewModel
{
    public int TrainerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public List<string> Specializations { get; set; } = new();
    public List<int> SpecializationIds { get; set; } = new();
    public int ActiveWorkload { get; set; }
    public DateTime HireDate { get; set; }
    public bool IsExactMatch { get; set; }
    public bool IsRecommended { get; set; }
}
