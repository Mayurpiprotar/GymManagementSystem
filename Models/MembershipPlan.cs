using System.ComponentModel.DataAnnotations;

namespace GymManagementSystem.Models;

public class MembershipPlan
{
    public int MembershipPlanId { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 120)]
    public int DurationInMonths { get; set; }

    [Range(0, 100000)]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Membership> Memberships { get; set; } = new List<Membership>();
}
