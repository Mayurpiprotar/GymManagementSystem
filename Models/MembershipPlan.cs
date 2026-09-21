namespace GymManagementSystem.Models;

public class MembershipPlan
{
    public int MembershipPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationInMonths { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
}
