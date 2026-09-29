namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// Top-level view model for the Admin Trainer Assignments index dashboard.
/// </summary>
public class TrainerAssignmentListViewModel
{
    public List<TrainerAssignmentListItemViewModel> EligibleMemberships { get; set; } = new();
    public List<IneligibleMembershipInfoViewModel> IneligibleMemberships { get; set; } = new();

    public string Filter { get; set; } = "all"; // "all", "unassigned", "assigned"
    public int TotalEligibleCount { get; set; }
    public int UnassignedCount { get; set; }
    public int AssignedCount { get; set; }
    public int IneligibleCount { get; set; }
}
