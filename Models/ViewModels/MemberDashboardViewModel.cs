using GymManagementSystem.Models;

namespace GymManagementSystem.Models.ViewModels;

public class MemberDashboardViewModel
{
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
    public bool HasMembership { get; set; }
    public Membership? CurrentMembership { get; set; }
    public List<Membership> AllMemberships { get; set; } = new();
    public List<Payment> PaymentHistory { get; set; } = new();
    public List<WorkoutPlan> WorkoutPlans { get; set; } = new();
}
