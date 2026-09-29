namespace GymManagementSystem.Models.ViewModels;

/// <summary>
/// Informational row item explaining why a particular membership is not currently eligible for trainer assignment.
/// </summary>
public class IneligibleMembershipInfoViewModel
{
    public int MembershipId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CanonicalStatus { get; set; } = string.Empty;
    public bool HasPaidPayment { get; set; }
    public bool HasTrainingGoal { get; set; }
    public string IneligibilityReason { get; set; } = string.Empty;
}
