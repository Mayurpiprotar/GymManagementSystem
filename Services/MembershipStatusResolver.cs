namespace GymManagementSystem.Services;

using GymManagementSystem.Models;

/// <summary>
/// Centralized business logic for resolving canonical membership statuses and assignment eligibility.
/// IMPORTANT: Membership.Status stored in the database is not authoritative for eligibility.
/// Canonical status is dynamically derived from verified payment records and date boundaries.
/// </summary>
public static class MembershipStatusResolver
{
    /// <summary>
    /// Evaluates whether the membership has at least one verified payment with Status == 'Paid'.
    /// Pending, Failed, or Refunded payments do NOT make a membership paid.
    /// </summary>
    public static bool IsPaidMembership(Membership? membership)
    {
        if (membership == null || membership.Payments == null || membership.Payments.Count == 0)
        {
            return false;
        }

        return membership.Payments.Any(p =>
            string.Equals(p.Status, GymConstants.PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Calculates the canonical membership status based on database facts:
    /// 1. If the membership does not have a linked 'Paid' payment: returns 'PendingPayment'.
    /// 2. If there is a linked 'Paid' payment and StartDate > today: returns 'Upcoming'.
    /// 3. If there is a linked 'Paid' payment and StartDate &lt;= today &lt;= EndDate: returns 'Active'.
    /// 4. If there is a linked 'Paid' payment and EndDate &lt; today: returns 'Expired'.
    /// Date-only comparisons (.Date) are used so time-of-day does not affect eligibility boundaries.
    /// </summary>
    public static string ResolveStatus(Membership? membership, DateTime? today = null)
    {
        if (membership == null)
        {
            return GymConstants.MembershipStatuses.PendingPayment;
        }

        bool isPaid = IsPaidMembership(membership);
        return ResolveStatus(membership.StartDate, membership.EndDate, isPaid, today);
    }

    /// <summary>
    /// Calculates canonical status based directly on date boundaries and paid status.
    /// </summary>
    public static string ResolveStatus(DateTime startDate, DateTime endDate, bool isPaid, DateTime? today = null)
    {
        // Rule 1: A membership without a verified Paid payment remains PendingPayment
        if (!isPaid)
        {
            return GymConstants.MembershipStatuses.PendingPayment;
        }

        var currentDate = (today ?? DateTime.Today).Date;
        var start = startDate.Date;
        var end = endDate.Date;

        // Rule 2: Paid membership whose start date is in the future
        if (start > currentDate)
        {
            return GymConstants.MembershipStatuses.Upcoming;
        }

        // Rule 3: Paid membership currently within its active validity window
        if (start <= currentDate && currentDate <= end)
        {
            return GymConstants.MembershipStatuses.Active;
        }

        // Rule 4: Paid membership whose end date has passed
        return GymConstants.MembershipStatuses.Expired;
    }

    /// <summary>
    /// Returns true only if the membership has a verified 'Paid' payment and today's date falls within [StartDate, EndDate].
    /// </summary>
    public static bool IsActiveMembership(Membership? membership, DateTime? today = null)
    {
        return ResolveStatus(membership, today) == GymConstants.MembershipStatuses.Active;
    }

    /// <summary>
    /// Determines whether a membership is eligible for trainer assignment.
    /// Rule: PAYMENT + CURRENT ACTIVE MEMBERSHIP + TRAINING GOAL are all required before trainer assignment.
    /// </summary>
    public static bool IsEligibleForTrainerAssignment(Membership? membership, DateTime? today = null)
    {
        return CanAssignTrainer(membership, out _, today);
    }

    /// <summary>
    /// Detailed evaluation of trainer assignment eligibility, providing an actionable failure reason when ineligible.
    /// Rules:
    /// 1. Membership has a valid Member (MemberId > 0)
    /// 2. Membership has a TrainingGoalSpecializationId
    /// 3. Membership has a linked paid payment
    /// 4. Canonical membership status is Active
    /// </summary>
    public static bool CanAssignTrainer(Membership? membership, out string failureReason, DateTime? today = null)
    {
        if (membership == null)
        {
            failureReason = "Membership record was not found.";
            return false;
        }

        if (membership.MemberId <= 0)
        {
            failureReason = "Membership is not associated with a valid member.";
            return false;
        }

        if (!membership.TrainingGoalSpecializationId.HasValue || membership.TrainingGoalSpecializationId.Value <= 0)
        {
            failureReason = "A training goal specialization must be selected before assigning a trainer.";
            return false;
        }

        if (!IsPaidMembership(membership))
        {
            failureReason = "Membership has not been paid. A verified payment is required before assigning a trainer.";
            return false;
        }

        var status = ResolveStatus(membership, today);
        if (status != GymConstants.MembershipStatuses.Active)
        {
            failureReason = $"Only canonically Active memberships can receive trainer assignment. Current status is '{status}'.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }
}
