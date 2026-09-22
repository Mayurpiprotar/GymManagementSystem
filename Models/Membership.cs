using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class Membership
{
    public int MembershipId { get; set; }

    public int MemberId { get; set; }

    public int MembershipPlanId { get; set; }

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    // Navigation properties
    public Member? Member { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
