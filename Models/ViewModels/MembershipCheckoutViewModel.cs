namespace GymManagementSystem.Models.ViewModels;

using GymManagementSystem.Models;

/// <summary>
/// ViewModel for the checkout review and demo payment gateway screens.
/// All monetary values, durations, and dates are populated strictly from server database records.
/// </summary>
public class MembershipCheckoutViewModel
{
    public int MembershipId { get; set; }
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;

    // Plan Information (Database authoritative)
    public int MembershipPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int PlanDurationInMonths { get; set; }
    public decimal PlanPrice { get; set; }
    public string? PlanDescription { get; set; }

    // Training Preferences
    public string TrainingGoalName { get; set; } = string.Empty;
    public string ExperienceLevel { get; set; } = string.Empty;
    public string TrainingPreference { get; set; } = string.Empty;

    // Membership Dates & Status
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CanonicalStatus { get; set; } = string.Empty;

    // Prior Payment Attempts (for retry history display)
    public List<Payment> PreviousPayments { get; set; } = new();

    // Coach Assignment (Preserved during renewal)
    public int? AssignedTrainerId { get; set; }
    public string? AssignedTrainerName { get; set; }
    public bool HasAssignedTrainer => AssignedTrainerId.HasValue && !string.IsNullOrEmpty(AssignedTrainerName);

    // Simulation parameter for demo payment
    public bool SimulateSuccess { get; set; } = true;

    // Referral and Renewal Discount Fields
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount => Math.Round(PlanPrice * (DiscountPercent / 100m), 2);
    public decimal FinalAmount => Math.Max(0, PlanPrice - DiscountAmount);
    public string? DiscountReason { get; set; }
}
