using GymManagementSystem.Models;

namespace GymManagementSystem.Models.ViewModels;

public class AdminDashboardViewModel
{
    // Metric summary counts
    public int TotalMembers { get; set; }
    public int TotalTrainers { get; set; }
    public int TotalMembershipPlans { get; set; }
    public int ActiveMemberships { get; set; }
    public int TotalPayments { get; set; }
    public int TotalWorkoutPlans { get; set; }

    // Recent data lists
    public List<Member> RecentMembers { get; set; } = new();
    public List<Payment> RecentPayments { get; set; } = new();
}
