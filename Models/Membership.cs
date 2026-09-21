namespace GymManagementSystem.Models;

public class Membership
{
    public int MembershipId { get; set; }
    public int MemberId { get; set; }
    public int MembershipPlanId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
